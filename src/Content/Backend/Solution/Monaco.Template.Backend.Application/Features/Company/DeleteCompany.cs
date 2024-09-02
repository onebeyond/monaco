using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Application.Infrastructure.Context;
#if (filesSupport)
using Monaco.Template.Backend.Application.Services.Contracts;
using Monaco.Template.Backend.Domain.Model;
#endif
using Monaco.Template.Backend.Common.Application.Commands;
using Monaco.Template.Backend.Common.Application.Validators.Extensions;

namespace Monaco.Template.Backend.Application.Features.Company;

public sealed class DeleteCompany
{
	public sealed record Command(Guid Id) : CommandBase(Id);

	internal sealed class Validator : AbstractValidator<Command>
	{
		public Validator(AppDbContext dbContext)
		{
			RuleLevelCascadeMode = CascadeMode.Stop;

			this.CheckIfExists<Command, Domain.Model.Company>(dbContext);
		}
	}

	internal sealed class Handler : IRequestHandler<Command, CommandResult>
	{
		private readonly AppDbContext _dbContext;
		#if (filesSupport)
		private readonly IFileService _fileService;
		#endif

		#if (filesSupport)
		public Handler(AppDbContext dbContext, IFileService fileService)
		#else
		public Handler(AppDbContext dbContext)
		#endif
		{
			_dbContext = dbContext;
			#if (filesSupport)
			_fileService = fileService;
			#endif
		}

		public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
		{
			var item = await _dbContext.Set<Domain.Model.Company>()
									   #if (filesSupport)
									   .Include(x => x.Products)
									   .ThenInclude(x => x.Pictures)
									   .ThenInclude(x => x.Thumbnail)
									   #endif
									   .SingleAsync(x => x.Id == request.Id,
													cancellationToken);
			#if (filesSupport)
			var pictures = item.Products
							   .SelectMany(x => x.Pictures)
							   .ToArray();

			#endif
			_dbContext.Set<Domain.Model.Company>()
					  .Remove(item);
			#if (filesSupport)
			_dbContext.Set<Image>()
					  .RemoveRange(pictures.Union(pictures.Select(x => x.Thumbnail!)
														  .ToArray()));

			#endif
			await _dbContext.SaveEntitiesAsync(cancellationToken);
			#if (filesSupport)

			await _fileService.DeleteImagesAsync(pictures, cancellationToken);
			#endif

			return CommandResult.Success();
		}
	}
}
