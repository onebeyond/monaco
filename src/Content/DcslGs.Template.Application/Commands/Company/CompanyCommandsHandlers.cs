using MediatR;
using Microsoft.EntityFrameworkCore;
using DcslGs.Template.Application.DTOs.Extensions;
using DcslGs.Template.Common.Application.Commands;
using DcslGs.Template.Common.Application.Commands.Contracts;
using DcslGs.Template.Domain.Model;
using DcslGs.Template.Infrastructure.Context;

namespace DcslGs.Template.Application.Commands.Company;

public class CompanyCommandsHandlers : IRequestHandler<CompanyCreateCommand, ICommandResult<Guid>>,
                                       IRequestHandler<CompanyEditCommand, ICommandResult>,
                                       IRequestHandler<CompanyDeleteCommand, ICommandResult>
{
    private readonly AppDbContext _dbContext;

    public CompanyCommandsHandlers(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ICommandResult<Guid>> Handle(CompanyCreateCommand request, CancellationToken cancellationToken)
    {
        var country = await GetCountry(request.CountryId, cancellationToken);
        var item = request.Map(country!);

        _dbContext.Set<Domain.Model.Company>().Attach(item);
        await _dbContext.SaveEntitiesAsync(cancellationToken);

        return new CommandResult<Guid>(item.Id);
    }

    public async Task<ICommandResult> Handle(CompanyEditCommand request, CancellationToken cancellationToken)
    {
        var item = await _dbContext.Set<Domain.Model.Company>()
                                   .FindAsync(new object[] {request.Id}, cancellationToken);
        var country = await GetCountry(request.CountryId, cancellationToken);

        item!.Update(request.Name,
                     request.Email,
                     request.WebSiteUrl,
                     request.Address,
                     request.City,
                     request.County,
                     request.PostCode,
                     country);
			
        await _dbContext.SaveEntitiesAsync(cancellationToken);

        return new CommandResult();
    }

    public async Task<ICommandResult> Handle(CompanyDeleteCommand request, CancellationToken cancellationToken)
    {
        var item = await _dbContext.Set<Domain.Model.Company>().SingleAsync(x => x.Id == request.Id, cancellationToken);
			
        _dbContext.Set<Domain.Model.Company>().Remove(item);
        await _dbContext.SaveEntitiesAsync(cancellationToken);

        return new CommandResult();
    }

    private async Task<Country> GetCountry(Guid countryId, CancellationToken cancellationToken)
    {
        return await _dbContext.Set<Country>().SingleAsync(x => x.Id == countryId, cancellationToken);
    }
}