﻿using AutoFixture;
using Monaco.Template.Backend.Domain.Model;
using Moq;

namespace Monaco.Template.Backend.Common.Tests.Factories.Entities;

public class DocumentFactory
{
	public static Document Create() =>
		new Fixture().RegisterDocument()
					 .Create<Document>();

	public static IEnumerable<Document> CreateMany() =>
		new Fixture().RegisterDocument()
					 .CreateMany<Document>();
}

public static class DocumentFactoryExtension
{
	public static IFixture RegisterDocument(this IFixture fixture)
	{
		fixture.Register(() => new Document(fixture.Create<Guid>(),
											fixture.Create<string>(),
											fixture.Create<string>(),
											fixture.Create<long>(),
											fixture.Create<string>(),
											false));
		return fixture;
	}

	public static IFixture RegisterDocumentMock(this IFixture fixture)
	{
		fixture.Register(() =>
						 {
							 var mock = new Mock<Document>(fixture.Create<Guid>(),
														   fixture.Create<string>(),
														   fixture.Create<string>(),
														   fixture.Create<long>(),
														   fixture.Create<string>(),
														   false);
							 return mock.Object;
						 });
		return fixture;
	}
}