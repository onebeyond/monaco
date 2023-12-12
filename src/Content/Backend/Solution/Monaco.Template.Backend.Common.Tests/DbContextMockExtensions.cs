using Microsoft.EntityFrameworkCore;
using MockQueryable.Moq;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Domain.Model;
using Moq;

namespace Monaco.Template.Backend.Common.Tests;

public static class DbContextMockExtensions
{
	public static Mock<AppDbContext> SetupDbSetMock<T>(this Mock<AppDbContext> dbContextMock, T entity) where T : Entity
	{
		var entityDbSetMock = new List<T> { entity }.AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<T>()).Returns(entityDbSetMock.Object);
		entityDbSetMock.Setup(x => x.FindAsync(new object[] { entity.Id }, It.IsAny<CancellationToken>()))
					   .ReturnsAsync(entity);
		return dbContextMock;
	}

	public static Mock<AppDbContext> CreateEntityMockAndSetupDbSetMock<T>(this Mock<AppDbContext> dbContextMock, out Mock<T> entityMock) where T : Entity
	{
		entityMock = new Mock<T>();
		var entityDbSetMock = new List<T> { entityMock.Object }.AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<T>()).Returns(entityDbSetMock.Object);
		entityDbSetMock.Setup(x => x.FindAsync(new object[] { It.IsAny<Guid>() }, It.IsAny<CancellationToken>()))
					   .ReturnsAsync(entityMock.Object);

		return dbContextMock;
	}

	public static Mock<AppDbContext> CreateEntityMockAndSetupDbSetMock<T>(this Mock<AppDbContext> dbContextMock) where T : Entity
		=> dbContextMock.CreateEntityMockAndSetupDbSetMock<T>(out _);

	public static Mock<AppDbContext> CreateAndSetupDbSetMock<T>(this Mock<AppDbContext> dbContextMock, T entity, out Mock<DbSet<T>> entityDbSetMock) where T : Entity
	{
		entityDbSetMock = new[] { entity }.AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<T>()).Returns(entityDbSetMock.Object);
		entityDbSetMock.Setup(x => x.FindAsync(new object[] { It.IsAny<Guid>() }, It.IsAny<CancellationToken>()))
					   .ReturnsAsync(entity);

		return dbContextMock;
	}

	public static Mock<AppDbContext> CreateAndSetupDbSetMock<T>(this Mock<AppDbContext> dbContextMock, T entity) where T : Entity
		=> dbContextMock.CreateAndSetupDbSetMock(entity, out _);

	public static Mock<AppDbContext> CreateAndSetupDbSetMock<T>(this Mock<AppDbContext> dbContextMock, IEnumerable<T> entities, out Mock<DbSet<T>> entityDbSetMock) where T : Entity
	{
		entityDbSetMock = entities.AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<T>()).Returns(entityDbSetMock.Object);

		return dbContextMock;
	}

	public static Mock<AppDbContext> CreateAndSetupDbSetMock<T>(this Mock<AppDbContext> dbContextMock, IEnumerable<T> entities) where T : Entity
		=> dbContextMock.CreateAndSetupDbSetMock(entities, out _);
}
