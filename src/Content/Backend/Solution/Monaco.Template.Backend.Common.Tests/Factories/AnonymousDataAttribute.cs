using AutoFixture;
using AutoFixture.Xunit2;

namespace Monaco.Template.Backend.Common.Tests.Factories;

public class AnonymousDataAttribute(bool mockedData = false) : AutoDataAttribute(() => mockedData
																						   ? new Fixture().RegisterMockFactories()
																						   : new Fixture().RegisterFactories());