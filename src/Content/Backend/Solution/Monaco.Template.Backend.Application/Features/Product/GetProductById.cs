using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Application.DTOs.Extensions;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Application.Queries;

namespace Monaco.Template.Backend.Application.Features.Product;


public sealed class GetProductById
{
	public sealed record Query(Guid Id) : QueryByIdBase<ProductDto?>(Id);

	internal sealed class Handler : IRequestHandler<Query, ProductDto?>
	{
		private readonly AppDbContext _dbContext;

		public Handler(AppDbContext dbContext)
		{
			_dbContext = dbContext;
		}

		public async Task<ProductDto?> Handle(Query request, CancellationToken cancellationToken)
		{
			var item = await _dbContext.Set<Domain.Model.Product>()
									   .AsNoTracking()
									   .Include(x => x.Company)
									   .SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
			return item.Map(true, true, true);
		}
	}
}
