#if filesSupport
using Microsoft.EntityFrameworkCore;

namespace Monaco.Template.Backend.Application.Features.File.Extensions;

internal static class FileExtensions
{
	internal static Task<Domain.Model.File?> GetFile(this DbContext dbContext, Guid id, CancellationToken cancellationToken) =>
		dbContext.Set<Domain.Model.File>()
				 .AsNoTracking()
				 .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
}
#endif