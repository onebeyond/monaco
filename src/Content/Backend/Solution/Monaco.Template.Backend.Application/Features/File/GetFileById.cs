using MediatR;
using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Application.DTOs.Extensions;
using Monaco.Template.Backend.Application.Features.File.Extensions;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Application.Queries;

namespace Monaco.Template.Backend.Application.Features.File;

public sealed class GetFileById
{
	public record Query(Guid Id) : QueryByIdBase<FileDto>(Id);

	public sealed class Handler : IRequestHandler<Query, FileDto?>
	{
		private readonly AppDbContext _dbContext;

		public Handler(AppDbContext dbContext)
		{
			_dbContext = dbContext;
		}

		public async Task<FileDto?> Handle(Query request, CancellationToken cancellationToken)
		{
			var item = await _dbContext.GetFile(request.Id, cancellationToken);
			return item.Map();
		}
	}
}