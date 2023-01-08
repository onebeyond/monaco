using Monaco.Template.Common.Application.Queries;
using Monaco.Template.Application.DTOs;

namespace Monaco.Template.Application.Features.Company.Queries;

public record GetCompanyByIdQuery(Guid Id) : QueryByIdBase<CompanyDto?>(Id);