namespace Diploma.Source.Mesh;

public readonly record struct DirichletCondition(int Node, double Value);

public readonly record struct NeumannCondition(int Element, double Power); 