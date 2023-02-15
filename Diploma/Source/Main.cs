using Diploma.Source;

var meshParameters = MeshParameters.ReadJson("Input/");
MeshBuilder meshBuilder = new(meshParameters);
Mesh mesh = meshBuilder.Build();
DataWriter.WriteMesh(mesh);

PhaseProperty phaseProperty = new(mesh, "Input/");
FEMBuilder femBuilder = new();

double Field(Point2D p) => 10 - p.X;
double Source(Point2D p) => 0.0;

var fem = femBuilder
    .SetMesh(mesh)
    .SetPhaseProperties(phaseProperty)
    .SetBasis(new LinearBasis())
    .SetSolver(new CGM(1000, 1E-20))
    .SetTest(Source)
    .Build();
        
fem.Solve();
            
DataWriter.WritePressure($"Pressure{0}.txt", fem.Solution!);
DataWriter.WriteSaturation($"Saturation{0}.txt", mesh, phaseProperty.Saturation!);

// Filtration filtration = new(mesh, phaseProperty, fem, new LinearBasis());
// filtration.ModelFiltration(0, 100);