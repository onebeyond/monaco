using FluentValidation;
#if (massTransitIntegration)
using MassTransit;
#endif
using MediatR;
using Monaco.Template.Backend.Application.DTOs.Extensions;
using Monaco.Template.Backend.Application.Features.Product.Extensions;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Application.Services.Contracts;
using Monaco.Template.Backend.Common.Application.Commands;
using Monaco.Template.Backend.Common.Application.Validators.Extensions;
using Monaco.Template.Backend.Common.Infrastructure.Context.Extensions;
#if (massTransitIntegration)
using Monaco.Template.Backend.Messages.V1;
#endif

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
				.MaximumLength(Domain.Model.Product.TitleLength)
				.MustAsync(async (title, ct) => !await dbContext.ExistsAsync<Domain.Model.Product>(x => x.Title == title, ct))
				.WithMessage("A product with the title {PropertyValue} already exists");

			RuleFor(x => x.Description)
				.NotEmpty()
				.MaximumLength(Domain.Model.Product.DescriptionLength);

			RuleFor(x => x.Price)
				.NotNull()
				.GreaterThanOrEqualTo(0);

			RuleFor(x => x.CompanyId)
				.NotEmpty()
				.MustExistAsync<Command, Domain.Model.Company, Guid>(dbContext);

			RuleFor(x => x.Pictures)
				.NotEmpty();

			RuleForEach(cmd => cmd.Pictures)
				.NotEmpty()
				.MustExistAsync<Command, Domain.Model.Image, Guid>(dbContext)
				.MustAsync(async (id, ct) => !await dbContext.ExistsAsync<Domain.Model.Product>(x => x.Pictures.Any(p => p.Id == id), ct))
				.WithMessage("This picture is already in use by another product");

			RuleFor(x => x.DefaultPictureId)
				.NotEmpty()
				.Must((cmd, id) => cmd.Pictures.Contains(id))
				.WithMessage("The default picture must exist in the Pictures array");
		}
	}

	internal sealed class Handler : IRequestHandler<Command, CommandResult<Guid>>
	{
		private readonly AppDbContext _dbContext;
#if (massTransitIntegration)
		private readonly IPublishEndpoint _publishEndpoint;
#endif
		private readonly IFileService _fileService;

		public Handler(AppDbContext dbContext,
#if (massTransitIntegration)
					   IPublishEndpoint publishEndpoint,
#endif
					   IFileService fileService)
		{
			_dbContext = dbContext;
#if (massTransitIntegration)
			_publishEndpoint = publishEndpoint;
#endif
			_fileService = fileService;
		}

		public async Task<CommandResult<Guid>> Handle(Command request, CancellationToken cancellationToken)
		{
			var (company, pictures) = await _dbContext.GetProductData(request.CompanyId, request.Pictures, cancellationToken);

			var item = request.Map(pictures);
			company.AddProduct(item);

			_dbContext.Set<Domain.Model.Product>().Attach(item);
			await _dbContext.SaveEntitiesAsync(cancellationToken);
#if (massTransitIntegration)

			//NOTE: It is strongly recommended to implement MT's Transactional Outbox for ensuring transaction of DB and message publishing
			await _publishEndpoint.Publish(item.MapMessage(), cancellationToken);
#endif

			await _fileService.MakePermanentImagesAsync(pictures, cancellationToken);

			return CommandResult<Guid>.Success(item.Id);
		}
	}
}