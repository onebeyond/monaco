using Microsoft.EntityFrameworkCore;

namespace Monaco.Template.Backend.Application.Features.Image.Extensions;

internal static class ImageExtensions
{
	internal static Task<Domain.Model.Image?> GetImage(this DbContext dbContext, Guid id, CancellationToken cancellationToken) =>
		dbContext.Set<Domain.Model.Image>()
				 .AsNoTracking()
				 .Include(x => x.Thumbnail)
				 .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

	internal static Task<Domain.Model.Image?> GetThumbnail(this DbContext dbContext, Guid id, CancellationToken cancellationToken) =>
		dbContext.Set<Domain.Model.Image>()
				 .AsNoTracking()
				 .Where(x => x.Id == id)
				 .Select(x => x.Thumbnail)
				 .SingleOrDefaultAsync(cancellationToken);
}