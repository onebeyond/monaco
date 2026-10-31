namespace Monaco.Eng.GeneratedShapeHarness;

public sealed record ShapeOptions(bool Api,
								  bool Worker,
								  bool Gateway,
								  bool Auth,
								  bool MassTransit,
								  bool Files,
								  bool Tests,
								  bool Common,
								  bool Keycloak);

public sealed record ShapeDefinition(string Id,
									 ShapeOptions Options,
									 IReadOnlyList<string> Arguments);

public static class ShapeCatalog
{
	public static ShapeOptions Default { get; } = new(true,
													  true,
													  false,
													  true,
													  true,
													  true,
													  true,
													  true,
													  true);

	public static IReadOnlyList<ShapeDefinition> All { get; } =
	[
		new("R1",
			new(true, true, false, true, true, true, true, true, true),
			[]),
		new("R2",
			new(true, true, true, true, true, true, true, true, true),
			["--apiGateway", "true"]),
		new("R3",
			new(true, false, false, false, false, false, true, true, false),
			["--workerService", "false", "--auth", "false", "--massTransitIntegration", "false", "--filesSupport", "false", "--keycloakConfig", "false"]),
		new("R4",
			new(true, false, false, false, true, true, true, true, false),
			["--workerService", "false", "--auth", "false", "--keycloakConfig", "false"]),
		new("R5",
			new(false, true, false, false, true, true, false, true, false),
			["--apiService", "false", "--auth", "false", "--tests", "false", "--keycloakConfig", "false"]),
		new("R6",
			new(false, true, false, false, false, false, false, true, false),
			["--apiService", "false", "--auth", "false", "--massTransitIntegration", "false", "--filesSupport", "false", "--tests", "false", "--keycloakConfig", "false"]),
		new("R7",
			new(true, true, false, true, true, true, false, true, true),
			["--tests", "false"]),
		new("E1",
			new(true, true, false, true, true, true, false, false, false),
			["--tests", "false", "--commonLibraries", "false", "--keycloakConfig", "false"]),
		new("E2",
			new(false, false, false, false, false, false, false, true, false),
			["--apiService", "false", "--workerService", "false", "--auth", "false", "--massTransitIntegration", "false", "--filesSupport", "false", "--tests", "false", "--keycloakConfig", "false"]),
		new("E3",
			new(false, false, true, false, false, false, false, true, false),
			["--apiService", "false", "--workerService", "false", "--apiGateway", "true", "--auth", "false", "--massTransitIntegration", "false", "--filesSupport", "false", "--tests", "false", "--keycloakConfig", "false"]),
		new("E4",
			new(false, true, false, false, true, true, true, true, false),
			["--apiService", "false", "--auth", "false", "--keycloakConfig", "false"])
	];

	public static ShapeOptions Apply(IReadOnlyList<string> arguments)
	{
		var options = Default;
		if (arguments.Count % 2 != 0)
			throw new ArgumentException("Option overrides must be flag/value pairs.", nameof(arguments));

		for (var index = 0; index < arguments.Count; index += 2)
		{
			var value = bool.Parse(arguments[index + 1]);
			options = arguments[index] switch
			{
				"--apiService" => options with { Api = value },
				"--workerService" => options with { Worker = value },
				"--apiGateway" => options with { Gateway = value },
				"--auth" => options with { Auth = value },
				"--massTransitIntegration" => options with { MassTransit = value },
				"--filesSupport" => options with { Files = value },
				"--tests" => options with { Tests = value },
				"--commonLibraries" => options with { Common = value },
				"--keycloakConfig" => options with { Keycloak = value },
				_ => throw new ArgumentException($"Unknown template option '{arguments[index]}'.", nameof(arguments))
			};
		}

		return options;
	}
}
