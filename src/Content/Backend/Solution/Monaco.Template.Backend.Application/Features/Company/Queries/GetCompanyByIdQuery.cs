using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Common.Application.Queries;

namespace Monaco.Template.Backend.Application.Features.Company.Queries;

public record GetCompanyByIdQuery(Guid Id) : QueryByIdBase<CompanyDto?>(Id);