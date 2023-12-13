using Microsoft.EntityFrameworkCore;
using MockQueryable.Moq;
using Monaco.Template.Backend.Common.Domain.Model;
using Moq;

namespace Monaco.Template.Backend.Common.Tests;

public static class DbContextMockExtensions
{
	public static Mock<TDbContext> SetupDbSetMock<TDbContext, T>(this Mock<TDbContext> dbContextMock, T entity)
		where TDbContext : DbContext
		where T : Entity
	{
		var entityDbSetMock = new List<T> { entity }.AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<T>()).Returns(entityDbSetMock.Object);
		entityDbSetMock.Setup(x => x.FindAsync(new object[] { entity.Id }, It.IsAny<CancellationToken>()))
					   .ReturnsAsync(entity);
		return dbContextMock;
	}

	public static Mock<TDbContext> CreateEntityMockAndSetupDbSetMock<TDbContext, T>(this Mock<TDbContext> dbContextMock, out Mock<T> entityMock)
		where TDbContext : DbContext
		where T : Entity
	{
		entityMock = new Mock<T>();
		var entityDbSetMock = new List<T> { entityMock.Object }.AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<T>()).Returns(entityDbSetMock.Object);
		entityDbSetMock.Setup(x => x.FindAsync(new object[] { It.IsAny<Guid>() }, It.IsAny<CancellationToken>()))
					   .ReturnsAsync(entityMock.Object);

		return dbContextMock;
	}

	public static Mock<TDbContext> CreateEntityMockAndSetupDbSetMock<TDbContext, T>(this Mock<TDbContext> dbContextMock)
		where TDbContext : DbContext
		where T : Entity
		=> dbContextMock.CreateEntityMockAndSetupDbSetMock<TDbContext, T>(out _);

	public static Mock<TDbContext> CreateAndSetupDbSetMock<TDbContext, T>(this Mock<TDbContext> dbContextMock, T entity, out Mock<DbSet<T>> entityDbSetMock)
		where TDbContext : DbContext
		where T : Entity
	{
		entityDbSetMock = new[] { entity }.AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<T>()).Returns(entityDbSetMock.Object);
		entityDbSetMock.Setup(x => x.FindAsync(new object[] { It.IsAny<Guid>() }, It.IsAny<CancellationToken>()))
					   .ReturnsAsync(entity);

		return dbContextMock;
	}

	public static Mock<TDbContext> CreateAndSetupDbSetMock<TDbContext, T>(this Mock<TDbContext> dbContextMock, T entity)
		where TDbContext : DbContext
		where T : Entity
		=> dbContextMock.CreateAndSetupDbSetMock(entity, out _);

	public static Mock<TDbContext> CreateAndSetupDbSetMock<TDbContext, T>(this Mock<TDbContext> dbContextMock, IEnumerable<T> entities, out Mock<DbSet<T>> entityDbSetMock)
		where TDbContext : DbContext
		where T : Entity
	{
		entityDbSetMock = entities.AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<T>()).Returns(entityDbSetMock.Object);

		return dbContextMock;
	}

	public static Mock<TDbContext> CreateAndSetupDbSetMock<TDbContext, T>(this Mock<TDbContext> dbContextMock, IEnumerable<T> entities)
		where TDbContext : DbContext
		where T : Entity
		=> dbContextMock.CreateAndSetupDbSetMock(entities, out _);
}
