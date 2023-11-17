using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Application.DTOs.Extensions;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Application.Commands;
using Monaco.Template.Backend.Common.Application.Commands.Contracts;
using Monaco.Template.Backend.Common.Infrastructure.Context.Extensions;

namespace Monaco.Template.Backend.Application.Features.Company.Commands;

public sealed class CompanyCommandsHandlers(AppDbContext dbContext) : IRequestHandler<CompanyCreateCommand, ICommandResult<Guid>>,
																	  IRequestHandler<CompanyEditCommand, ICommandResult>,
																	  IRequestHandler<CompanyDeleteCommand, ICommandResult>
{
	public async Task<ICommandResult<Guid>> Handle(CompanyCreateCommand request, CancellationToken cancellationToken)
	{
		var country = await GetCountry(request.CountryId, cancellationToken);
		var item = request.Map(country);

		dbContext.Set<Domain.Model.Company>().Attach(item);
		await dbContext.SaveEntitiesAsync(cancellationToken);

		return new CommandResult<Guid>(item.Id);
	}

	public async Task<ICommandResult> Handle(CompanyEditCommand request, CancellationToken cancellationToken)
	{
		var item = await dbContext.Set<Domain.Model.Company>().SingleAsync(x => x.Id == request.Id, cancellationToken);
		var country = await GetCountry(request.CountryId, cancellationToken);

		request.Map(item, country);

		await dbContext.SaveEntitiesAsync(cancellationToken);

		return new CommandResult();
	}

	public async Task<ICommandResult> Handle(CompanyDeleteCommand request, CancellationToken cancellationToken)
	{
		var item = await dbContext.Set<Domain.Model.Company>().SingleAsync(x => x.Id == request.Id, cancellationToken);

		dbContext.Set<Domain.Model.Company>().Remove(item);
		await dbContext.SaveEntitiesAsync(cancellationToken);

		return new CommandResult();
	}

	private async Task<Domain.Model.Country?> GetCountry(Guid? countryId, CancellationToken cancellationToken) =>
		await dbContext.GetAsync<Domain.Model.Country>(countryId, cancellationToken);
}