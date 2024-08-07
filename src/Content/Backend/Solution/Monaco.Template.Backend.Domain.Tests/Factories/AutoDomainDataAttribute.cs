using AutoFixture.Xunit2;
using Monaco.Template.Backend.Common.Tests;

namespace Monaco.Template.Backend.Domain.Tests.Factories;

public class AutoDomainDataAttribute(bool mocked = false)
	: AutoDataAttribute(() => mocked
								  ? FixtureFactory.Create(f => f.RegisterMockFactories())
								  : FixtureFactory.Create(f => f.RegisterEntityFactories()));