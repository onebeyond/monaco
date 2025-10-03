using FluentValidation;
#if (massTransitIntegration)
using MassTransit;
#endif
using MediatR;
using Monaco.Template.Backend.Application.Features.Product.Extensions;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Common.Application.Commands;
using Monaco.Template.Backend.Common.Application.Validators.Extensions;
using Monaco.Template.Backend.Common.Infrastructure.Context.Extensions;
using Monaco.Template.Backend.Domain.Model.Entities;

namespace Monaco.Template.Backend.Application.Features.Product;

public sealed class CreateProduct
{
	public sealed record Command(string Title,
								 string Description,
								 decimal Price,
								 Guid CompanyId,
								 Guid[] Pictures,
								 Guid DefaultPictureId) : CommandBase<Guid>;

	internal sealed class Validator : AbstractValidator<Command>
	{
		public Validator(AppDbContext dbContext)
		{
			RuleLevelCascadeMode = CascadeMode.Stop;

			RuleFor(x => x.Title)
				.NotEmpty()
				.MaximumLength(Domain.Model.Entities.Product.TitleLength)
				.MustAsync(async (title, ct) => !await dbContext.ExistsAsync<Domain.Model.Entities.Product>(x => x.Title == title, ct))
				.WithMessage("A product with the title {PropertyValue} already exists");

			RuleFor(x => x.Description)
				.NotEmpty()
				.MaximumLength(Domain.Model.Entities.Product.DescriptionLength);

			RuleFor(x => x.Price)
				.NotNull()
				.GreaterThanOrEqualTo(0);

			RuleFor(x => x.CompanyId)
				.NotEmpty()
				.MustExistAsync<Command, Domain.Model.Entities.Company, Guid>(dbContext);

			RuleFor(x => x.Pictures)
				.NotEmpty();

			RuleForEach(cmd => cmd.Pictures)
				.NotEmpty()
				.MustExistAsync<Command, Image, Guid>(dbContext)
				.MustAsync(async (id, ct) => !await dbContext.ExistsAsync<Domain.Model.Entities.Product>(x => x.Pictures.Any(p => p.Id == id), ct))
				.WithMessage("This picture is already in use by another product");

			RuleFor(x => x.DefaultPictureId)
				.NotEmpty()
				.Must((cmd, id) => cmd.Pictures.Contains(id))
				.WithMessage("The default picture must exist in the Pictures list");
		}
	}

	internal sealed class Handler : IRequestHandler<Command, CommandResult<Guid>>
	{
		private readonly AppDbContext _dbContext;
#if (massTransitIntegration)
		private readonly IPublishEndpoint _publishEndpoint;
#endif

#if (massTransitIntegration)
		public Handler(AppDbContext dbContext, IPublishEndpoint publishEndpoint)
#else
		public Handler(AppDbContext dbContext)
#endif
		{
			_dbContext = dbContext;
#if (massTransitIntegration)
			_publishEndpoint = publishEndpoint;
#endif
		}

		public async Task<CommandResult<Guid>> Handle(Command request, CancellationToken cancellationToken)
		{
			var (company, pictures) = await _dbContext.GetProductData(request.CompanyId, request.Pictures, cancellationToken);

			var item = new Domain.Model.Entities.Product(request.Title,
														 request.Description,
														 request.Price,
														 company,
														 [.. pictures],
														 pictures.Single(x => x.Id == request.DefaultPictureId));

			_dbContext.Set<Domain.Model.Entities.Product>().Attach(item);
#if (massTransitIntegration)

			await _publishEndpoint.Publish(item.MapMessage(), cancellationToken);
#endif

			await _dbContext.SaveEntitiesAsync(cancellationToken);

			return CommandResult<Guid>.Success(item.Id);
		}
	}
}