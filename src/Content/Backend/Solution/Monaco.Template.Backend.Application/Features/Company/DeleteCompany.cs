using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Application.Infrastructure.Context;
#if !excludeFilesSupport
using Monaco.Template.Backend.Application.Services.Contracts;
using Monaco.Template.Backend.Domain.Model;
#endif
using Monaco.Template.Backend.Common.Application.Commands;
using Monaco.Template.Backend.Common.Application.Commands.Contracts;
using Monaco.Template.Backend.Common.Application.Validators.Extensions;

namespace Monaco.Template.Backend.Application.Features.Company;

public sealed class DeleteCompany
{
	public record Command(Guid Id) : CommandBase(Id);

	public sealed class Validator : AbstractValidator<Command>
	{
		public Validator(AppDbContext dbContext)
		{
			RuleLevelCascadeMode = CascadeMode.Stop;

			this.CheckIfExists<Command, Domain.Model.Company>(dbContext);
		}
	}

	public sealed class Handler : IRequestHandler<Command, ICommandResult>
	{
		private readonly AppDbContext _dbContext;
		#if !excludeFilesSupport
		private readonly IFileService _fileService;
		#endif

		#if !excludeFilesSupport
		public Handler(AppDbContext dbContext, IFileService fileService)
		#else
		public Handler(AppDbContext dbContext)
		#endif
		{
			_dbContext = dbContext;
			#if !excludeFilesSupport
			_fileService = fileService;
			#endif
		}

		public async Task<ICommandResult> Handle(Command request, CancellationToken cancellationToken)
		{
			var item = await _dbContext.Set<Domain.Model.Company>()
									   .Include(x => x.Products)
									   #if !excludeFilesSupport
									   .ThenInclude(x => x.Pictures)
									   .ThenInclude(x => x.Thumbnail)
									   #endif
									   .SingleAsync(x => x.Id == request.Id,
													cancellationToken);
			#if !excludeFilesSupport
			var pictures = item.Products
							   .SelectMany(x => x.Pictures)
							   .ToArray();

			#endif
			_dbContext.Set<Domain.Model.Company>()
					  .Remove(item);
			#if !excludeFilesSupport
			_dbContext.Set<Image>()
					  .RemoveRange(pictures.Union(pictures.Select(x => x.Thumbnail!)
														  .ToArray()));

			#endif
			await _dbContext.SaveEntitiesAsync(cancellationToken);
			#if !excludeFilesSupport

			await _fileService.DeleteImagesAsync(pictures, cancellationToken);
			#endif

			return new CommandResult();
		}
	}
}
