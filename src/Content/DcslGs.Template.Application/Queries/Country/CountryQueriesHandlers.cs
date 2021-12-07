using MediatR;
using DcslGs.Template.Application.DTOs;
using DcslGs.Template.Application.DTOs.Extensions;
using DcslGs.Template.Common.Application.Queries.Extensions;
using DcslGs.Template.Infrastructure.Context;

namespace DcslGs.Template.Application.Queries.Country;

public class CountryQueriesHandlers : IRequestHandler<GetCountryListQuery, List<CountryDto>>,
                                      IRequestHandler<GetCountryByIdQuery, CountryDto>
{
    private readonly AppDbContext _dbContext;

    public CountryQueriesHandlers(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<CountryDto>> Handle(GetCountryListQuery request, CancellationToken cancellationToken)
    {
        var items = await request.ExecuteQueryAsync(_dbContext,
                                                    x => x.Map(),
                                                    nameof(CountryDto.Name),
                                                    CountryExtensions.GetMappedFields(),
                                                    cancellationToken);
        return items;
    }

    public async Task<CountryDto> Handle(GetCountryByIdQuery request, CancellationToken cancellationToken)
    {
        var item = await request.ExecuteQueryAsync<Domain.Model.Country, CountryDto>(_dbContext,
                                                                                     x => x.Map(),
                                                                                     cancellationToken);
        return item;
    }
}