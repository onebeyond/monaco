using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Application.Services.Contracts;
using Monaco.Template.Backend.Common.Application.Commands;
using Monaco.Template.Backend.Common.Application.Validators.Extensions;
using Monaco.Template.Backend.Domain.Model;

namespace Monaco.Template.Backend.Application.Features.Product;

public class DeleteProduct
{
	public record Command(Guid Id) : CommandBase(Id);

	public sealed class Validator : AbstractValidator<Command>
	{
		public Validator(AppDbContext dbContext)
		{
			RuleLevelCascadeMode = CascadeMode.Stop;

			this.CheckIfExists<Command, Domain.Model.Product>(dbContext);
		}
	}

	public sealed class Handler : IRequestHandler<Command, CommandResult>
	{
		private readonly AppDbContext _dbContext;
		private readonly IFileService _fileService;

		public Handler(AppDbContext dbContext, IFileService fileService)
		{
			_dbContext = dbContext;
			_fileService = fileService;
		}

		public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
		{
			var item = await _dbContext.Set<Domain.Model.Product>()
									   .Include(x => x.Pictures)
									   .ThenInclude(x => x.Thumbnail)
									   .SingleAsync(x => x.Id == request.Id, cancellationToken);

			var deletedPictures = item.Pictures
									  .Union(item.Pictures
												 .Select(x => x.Thumbnail!)
												 .ToArray());

			_dbContext.Set<Domain.Model.Product>()
					  .Remove(item);
			_dbContext.Set<Image>()
					  .RemoveRange(deletedPictures);

			await _dbContext.SaveEntitiesAsync(cancellationToken);

			await _fileService.DeleteImagesAsync([.. item.Pictures], cancellationToken);

			return CommandResult.Success();
		}
	}
}