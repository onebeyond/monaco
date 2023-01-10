using Monaco.Template.Application.DTOs;
using Monaco.Template.Common.Application.Queries;

namespace Monaco.Template.Application.Features.Company.Queries;

public record GetCompanyByIdQuery(Guid Id) : QueryByIdBase<CompanyDto?>(Id);