using Monaco.Eng.RuntimeShapeVerification.Fixtures;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Eng.RuntimeShapeVerification;

[ExcludeFromCodeCoverage]
[CollectionDefinition("RuntimeShapeVerification")]
public sealed class RuntimeShapeVerificationCollection : ICollectionFixture<ShapeHiveFixture>, ICollectionFixture<InfrastructureFixture>;