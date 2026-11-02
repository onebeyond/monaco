using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Monaco.Eng.FaultOtlpReceiver;

public static class Program
{
	public static async Task Main()
	{
		var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
		Console.SetOut(stdout);

		var dataPort = ReserveLoopbackPort();
		var control = new TcpListener(IPAddress.Loopback, 0);
		control.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
		control.Start();
		var controlPort = ((IPEndPoint)control.LocalEndpoint).Port;
		var server = new FaultServer(dataPort);

		Console.WriteLine($"data=http://127.0.0.1:{dataPort}");
		Console.WriteLine($"control=http://127.0.0.1:{controlPort}");

		while (true)
		{
			var client = await control.AcceptTcpClientAsync();
			try
			{
				await server.HandleControlAsync(client);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"control-fault {ex.GetType().Name}");
			}
			finally
			{
				CloseAfterSend(client);
			}
		}
	}

	private static void CloseAfterSend(TcpClient client)
	{
		try
		{
			client.LingerState = new(true, 1);
		}
		catch (SocketException)
		{
		}

		client.Dispose();
	}

	private static int ReserveLoopbackPort()
	{
		var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var port = ((IPEndPoint)listener.LocalEndpoint).Port;
		listener.Stop();
		return port;
	}
}

internal sealed class FaultServer
{
	private static readonly HashSet<string> KnownModes = new(StringComparer.Ordinal)
	{
		"refuse",
		"timeout",
		"401",
		"403",
		"429",
		"malformed",
		"saturate",
		"shutdown",
		"healthy"
	};

	private readonly int _dataPort;
	private readonly Lock _gate = new();
	private readonly List<FaultPayload> _payloads = [];
	private readonly List<TcpClient> _held = [];
	private string _mode = "refuse";
	private TcpListener? _dataListener;
	private CancellationTokenSource? _dataCts;
	private int _accepted;
	private int _completed;
	private int _inFlight;
	private int _retained;
	private int _responseStatus;
	private byte[] _responseBody = [];

	public FaultServer(int dataPort) =>
		_dataPort = dataPort;

	public async Task HandleControlAsync(TcpClient client)
	{
		var stream = client.GetStream();
		var request = await HttpExchange.ReadAsync(stream, CancellationToken.None);
		if (request is null)
			return;

		if (request.Method == "POST" && request.Path == "/mode")
		{
			var mode = Encoding.UTF8.GetString(request.Body).Trim();
			if (!KnownModes.Contains(mode))
			{
				await HttpExchange.WriteAsync(stream, 400, "Bad Request", "unknown mode"u8.ToArray(), "text/plain");
				return;
			}

			ApplyMode(mode);
			await HttpExchange.WriteAsync(stream, 200, "OK", "ok"u8.ToArray(), "text/plain");
			return;
		}

		if (request.Method == "GET" && request.Path == "/status")
		{
			var json = JsonBytes(Status());
			await HttpExchange.WriteAsync(stream, 200, "OK", json, "application/json");
			return;
		}

		if (request.Method == "GET" && request.Path == "/payloads")
		{
			var json = JsonBytes(SnapshotPayloads());
			await HttpExchange.WriteAsync(stream, 200, "OK", json, "application/json");
			return;
		}

		if (request.Method == "GET" && request.Path == "/response")
		{
			var json = JsonBytes(LastResponse());
			await HttpExchange.WriteAsync(stream, 200, "OK", json, "application/json");
			return;
		}

		await HttpExchange.WriteAsync(stream, 404, "Not Found", "missing"u8.ToArray(), "text/plain");
	}

	private void ApplyMode(string mode)
	{
		StopDataListener();
		Volatile.Write(ref _mode, mode);
		if (mode == "healthy")
		{
			lock (_gate)
				_payloads.Clear();
			Interlocked.Exchange(ref _retained, 0);
		}

		if (mode is "refuse")
			return;

		if (mode is "shutdown")
		{
			// The port is opened after the process is up, then closed. Refusal never binds it.
			StartDataListener();
			StopDataListener();
			return;
		}

		StartDataListener();
		Console.Error.WriteLine($"mode={mode}");
	}

	private void StartDataListener()
	{
		var listener = new TcpListener(IPAddress.Loopback, _dataPort);
		listener.Server.ExclusiveAddressUse = false;
		listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
		listener.Start();
		_dataListener = listener;
		_dataCts = new CancellationTokenSource();
		var token = _dataCts.Token;
		_ = Task.Run(() => AcceptLoopAsync(listener, token));
	}

	private void StopDataListener()
	{
		_dataCts?.Cancel();
		if (_dataListener is not null)
		{
			try
			{
				_dataListener.Stop();
			}
			catch (SocketException)
			{
			}

			_dataListener = null;
		}

		TcpClient[] held;
		lock (_gate)
		{
			held = _held.ToArray();
			_held.Clear();
		}

		foreach (var client in held)
			client.Dispose();

		Interlocked.Exchange(ref _inFlight, 0);
	}

	private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			TcpClient client;
			try
			{
				client = await listener.AcceptTcpClientAsync(cancellationToken);
			}
			catch (Exception) when (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			catch (SocketException) when (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			catch (ObjectDisposedException)
			{
				break;
			}

			_ = Task.Run(() => HandleDataAsync(client));
		}
	}

	private async Task HandleDataAsync(TcpClient client)
	{
		Interlocked.Increment(ref _accepted);
		var mode = Volatile.Read(ref _mode);
		if (mode == "saturate")
		{
			Interlocked.Increment(ref _inFlight);
			Hold(client);
			return;
		}

		try
		{
			using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
			var stream = client.GetStream();
			var request = await HttpExchange.ReadAsync(stream, timeout.Token);
			if (request is null)
			{
				client.Dispose();
				return;
			}

			Remember(request);
			if (mode == "timeout")
			{
				Interlocked.Increment(ref _inFlight);
				Hold(client);
				return;
			}

			var (status, reason, body) = ResponseFor(mode);
			if (mode == "healthy")
				Interlocked.Increment(ref _retained);

			await HttpExchange.WriteAsync(stream, status, reason, body, "application/octet-stream");
			RememberResponse(status, body);
			Interlocked.Increment(ref _completed);
			CloseAfterResponse(client);
		}
		catch
		{
			client.Dispose();
		}
	}

	private static void CloseAfterResponse(TcpClient client)
	{
		try
		{
			client.LingerState = new(true, 1);
		}
		catch (SocketException)
		{
		}

		client.Dispose();
	}

	private void Hold(TcpClient client)
	{
		lock (_gate)
			_held.Add(client);
	}

	private void Remember(HttpRequestData request)
	{
		lock (_gate)
			_payloads.Add(new(request.Path, request.Body));
	}

	private void RememberResponse(int status, byte[] body)
	{
		lock (_gate)
		{
			_responseStatus = status;
			_responseBody = body;
		}
	}

	private FaultDataResponse LastResponse()
	{
		lock (_gate)
			return new(_responseStatus, _responseBody);
	}

	private FaultReceiverStatus Status() =>
		new(Volatile.Read(ref _mode),
			Volatile.Read(ref _accepted),
			Volatile.Read(ref _completed),
			Volatile.Read(ref _inFlight),
			Volatile.Read(ref _retained));

	private FaultPayload[] SnapshotPayloads()
	{
		lock (_gate)
			return _payloads.ToArray();
	}

	private static (int Status, string Reason, byte[] Body) ResponseFor(string mode) =>
		mode switch
		{
			"401" => (401, "Unauthorized", CanaryBody()),
			"403" => (403, "Forbidden", CanaryBody()),
			"429" => (429, "Too Many Requests", "slow down"u8.ToArray()),
			"malformed" => (200, "OK", "not-protobuf"u8.ToArray()),
			"healthy" => (200, "OK", []),
			_ => (500, "Internal Server Error", "unexpected mode"u8.ToArray())
		};

	private static byte[] CanaryBody() =>
		Encoding.UTF8.GetBytes($"credential={FaultCanaries.Credential}\nprose={FaultCanaries.Prose}\n");

	private static byte[] JsonBytes<T>(T value) =>
		System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value, FaultReceiverJson.Options);
}

internal sealed record HttpRequestData(string Method, string Path, byte[] Body);

internal static class HttpExchange
{
	internal static async Task<HttpRequestData?> ReadAsync(Stream stream, CancellationToken cancellationToken)
	{
		var buffer = new MemoryStream();
		var chunk = new byte[8192];
		var headerEnd = -1;
		while (headerEnd < 0 && buffer.Length < 65_536)
		{
			var read = await stream.ReadAsync(chunk, cancellationToken);
			if (read == 0)
				return buffer.Length == 0 ? null : throw new InvalidOperationException("Incomplete HTTP request.");

			buffer.Write(chunk, 0, read);
			headerEnd = FindHeaderEnd(buffer.GetBuffer(), (int)buffer.Length);
		}

		if (headerEnd < 0)
			throw new InvalidOperationException("HTTP headers exceeded the limit.");

		var headerText = Encoding.ASCII.GetString(buffer.GetBuffer(), 0, headerEnd - 4);
		var lines = headerText.Split("\r\n");
		var requestLine = lines[0].Split(' ');
		if (requestLine.Length < 2)
			throw new InvalidOperationException("HTTP request line was incomplete.");

		var contentLength = 0;
		var chunked = false;
		var expectContinue = false;
		foreach (var line in lines.Skip(1))
		{
			if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
				contentLength = int.Parse(line[(line.IndexOf(':') + 1)..].Trim(), CultureInfo.InvariantCulture);
			else if (line.StartsWith("Transfer-Encoding:", StringComparison.OrdinalIgnoreCase) &&
					 line.Contains("chunked", StringComparison.OrdinalIgnoreCase))
				chunked = true;
			else if (line.StartsWith("Expect:", StringComparison.OrdinalIgnoreCase) &&
					 line.Contains("100-continue", StringComparison.OrdinalIgnoreCase))
				expectContinue = true;
		}

		var alreadyCount = (int)buffer.Length - headerEnd;
		var already = alreadyCount > 0 ? buffer.GetBuffer().AsSpan(headerEnd, alreadyCount).ToArray() : [];
		if (expectContinue && contentLength > already.Length)
		{
			await stream.WriteAsync("HTTP/1.1 100 Continue\r\n\r\n"u8.ToArray(), cancellationToken);
			await stream.FlushAsync(cancellationToken);
		}

		byte[] body;
		if (chunked)
			body = await ReadChunkedAsync(stream, already, cancellationToken);
		else
			body = await ReadContentLengthAsync(stream, already, contentLength, cancellationToken);

		var path = requestLine[1];
		var query = path.IndexOf('?', StringComparison.Ordinal);
		if (query >= 0)
			path = path[..query];

		return new(requestLine[0], path, body);
	}

	internal static async Task WriteAsync(Stream stream, int status, string reason, byte[] body, string contentType)
	{
		var head = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {reason}\r\nContent-Type: {contentType}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
		await stream.WriteAsync(head);
		if (body.Length > 0)
			await stream.WriteAsync(body);
		await stream.FlushAsync();
	}

	private static async Task<byte[]> ReadContentLengthAsync(Stream stream, byte[] already, int contentLength, CancellationToken cancellationToken)
	{
		if (contentLength <= 0)
			return [];

		if (contentLength > 32 * 1024 * 1024)
			throw new InvalidOperationException("HTTP body exceeded the limit.");

		var body = new byte[contentLength];
		var copied = Math.Min(already.Length, contentLength);
		if (copied > 0)
			already.AsSpan(0, copied).CopyTo(body);

		var offset = copied;
		while (offset < contentLength)
		{
			var read = await stream.ReadAsync(body.AsMemory(offset, contentLength - offset), cancellationToken);
			if (read == 0)
				throw new InvalidOperationException("Incomplete HTTP body.");

			offset += read;
		}

		return body;
	}

	private static async Task<byte[]> ReadChunkedAsync(Stream stream, byte[] already, CancellationToken cancellationToken)
	{
		var pending = new MemoryStream();
		pending.Write(already);
		pending.Position = 0;
		var result = new MemoryStream();
		while (true)
		{
			var line = await ReadLineAsync(pending, stream, cancellationToken);
			var sizeText = line.Split(';')[0].Trim();
			var size = int.Parse(sizeText, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			if (size == 0)
				return result.ToArray();

			if (size > 32 * 1024 * 1024)
				throw new InvalidOperationException("HTTP chunk exceeded the limit.");

			var chunk = await ReadExactFromPendingAsync(pending, stream, size, cancellationToken);
			result.Write(chunk);
			await ReadLineAsync(pending, stream, cancellationToken);
		}
	}

	private static async Task<string> ReadLineAsync(MemoryStream pending, Stream stream, CancellationToken cancellationToken)
	{
		var line = new MemoryStream();
		while (true)
		{
			var value = await ReadByteAsync(pending, stream, cancellationToken);
			if (value < 0)
				throw new InvalidOperationException("Incomplete HTTP chunk line.");

			if (value == '\n')
				break;

			if (value != '\r')
				line.WriteByte((byte)value);
		}

		return Encoding.ASCII.GetString(line.ToArray());
	}

	private static async Task<byte[]> ReadExactFromPendingAsync(MemoryStream pending, Stream stream, int count, CancellationToken cancellationToken)
	{
		var buffer = new byte[count];
		var offset = 0;
		while (offset < count)
		{
			var value = await ReadByteAsync(pending, stream, cancellationToken);
			if (value < 0)
				throw new InvalidOperationException("Incomplete HTTP chunk.");

			buffer[offset++] = (byte)value;
		}

		return buffer;
	}

	private static async Task<int> ReadByteAsync(MemoryStream pending, Stream stream, CancellationToken cancellationToken)
	{
		if (pending.Position < pending.Length)
			return pending.ReadByte();

		var one = new byte[1];
		var read = await stream.ReadAsync(one, cancellationToken);
		return read == 0 ? -1 : one[0];
	}

	private static int FindHeaderEnd(byte[] buffer, int length)
	{
		for (var index = 0; index <= length - 4; index++)
			if (buffer[index] == '\r' &&
				buffer[index + 1] == '\n' &&
				buffer[index + 2] == '\r' &&
				buffer[index + 3] == '\n')
				return index + 4;

		return -1;
	}
}
