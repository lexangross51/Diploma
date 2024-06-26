﻿namespace Diploma.Source.Mesh;

public class MeshGenerator
{
    private readonly IMeshBuilder _builder;

    public MeshGenerator(IMeshBuilder builder) => _builder = builder;

    public Mesh CreateMesh() => new(
        _builder.CreatePoints(),
        _builder.CreateElements(),
        _builder.CreateMaterials(),
        _builder.CreateDirichlet(),
        _builder.CreateNeumann()
    );
}