namespace Monaco.Template.Backend.Common.Application.DTOs;

/// <summary>
/// Represents the data required for a file download, including the file content, name, and content type.
/// </summary>
/// <remarks>This record is typically used to encapsulate file download information in APIs or services.</remarks>
/// <param name="FileContent">Contains the file's binary data.</param>
/// <param name="FileName">Specifies the name of the file as it should appear to the user.</param>
/// <param name="ContentType">indicates the MIME type of the file.</param>
public record FileDownloadDto(Stream FileContent,
							  string FileName,
							  string ContentType);