using FluentValidation;
using LinqKit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Application.Features.Product.Extensions;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Common.Application.Commands;
using Monaco.Template.Backend.Common.Application.Validators.Extensions;
using Monaco.Template.Backend.Common.Infrastructure.Context.Extensions;
using Monaco.Template.Backend.Domain.Model.Entities;

namespace Monaco.Template.Backend.Application.Features.Product;

public sealed class EditProduct
{
	public sealed record Command(Guid Id,
								 string Title,
								 string Description,
								 decimal Price,
								 Guid CompanyId,
								 Guid[] Pictures,
								 Guid DefaultPictureId) : CommandBase(Id);

	internal sealed class Validator : AbstractValidator<Command>
	{
		public Validator(AppDbContext dbContext)
		{
			RuleLevelCascadeMode = CascadeMode.Stop;

			this.CheckIfExists<Command, Domain.Model.Entities.Product>(dbContext);

			RuleFor(x => x.Title)
				.NotEmpty()
				.MaximumLength(Domain.Model.Entities.Product.TitleLength)
				.MustAsync(async (cmd, title, ct) => !await dbContext.ExistsAsync<Domain.Model.Entities.Product>(x => x.Id != cmd.Id &&
																											 x.Title == title,
																										ct))
				.WithMessage("Another product with the title {PropertyValue} already exists");

			RuleFor(x => x.Description)
				.NotEmpty()
				.MaximumLength(Domain.Model.Entities.Product.DescriptionLength);

			RuleFor(x => x.Price)
				.NotNull()
				.GreaterThanOrEqualTo(0);

			RuleFor(x => x.CompanyId)
				.NotEmpty()
				.MustExistAsync<Command, Domain.Model.Entities.Company>(dbContext);

			RuleFor(x => x.Pictures)
				.NotEmpty();

			RuleForEach(cmd => cmd.Pictures)
				.NotEmpty()
				.MustExistAsync<Command, Image>(dbContext)
				.MustAsync(async (cmd, id, ct) => !await dbContext.ExistsAsync<Domain.Model.Entities.Product>(x => x.Id != cmd.Id &&
																												   x.Pictures.Any(p => p.Id == id),
																											  ct))
				.WithMessage("This picture is already in use by another product");

			RuleFor(x => x.DefaultPictureId)
				.NotEmpty()
				.Must((cmd, id) => cmd.Pictures.Contains(id))
				.WithMessage("The default picture must exist in the Pictures list");
		}
	}

	internal sealed class Handler : IRequestHandler<Command, CommandResult>
	{
		private readonly AppDbContext _dbContext;

		public Handler(AppDbContext dbContext)
		{
			_dbContext = dbContext;
		}

		public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
		{
			var item = await _dbContext.Set<Domain.Model.Entities.Product>()
									   .Include(x => x.Company)
									   .Include(x => x.Pictures)
									   .ThenInclude(x => x.Thumbnail)
									   .SingleAsync(x => x.Id == request.Id, cancellationToken);
			
			var (company, pictures) = await _dbContext.GetProductData(request.CompanyId, request.Pictures, cancellationToken);

			item.Update(request.Title,
						request.Description,
						request.Price,
						company);

			pictures.ForEach(item.AddPicture);
			item.SetDefaultPicture(pictures.Single(x => x.Id == request.DefaultPictureId));
			var picturesToRemove = item.Pictures
									   .Where(p => !pictures.Contains(p))
									   .ToList();
			picturesToRemove.ForEach(item.RemovePicture);

			await _dbContext.SaveEntitiesAsync(cancellationToken);
			
			return CommandResult.Success();
		}
	}
}