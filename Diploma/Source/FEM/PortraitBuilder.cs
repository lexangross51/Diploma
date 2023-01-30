namespace Diploma.Source.FEM;

public static class PortraitBuilder
{
    public static void PortraitByNodes(Mesh.Mesh mesh, out int[] ig, out int[] jg)
    {
        var connectivityList = new List<HashSet<int>>();

        for (int i = 0; i < mesh.Points.Length; i++)
        {
            connectivityList.Add(new HashSet<int>());
        }

        int localSize = mesh.Elements[0].Nodes.Count;
        int[] nodes = new int[localSize];
        
        foreach (var element in mesh.Elements)
        {
            element.Nodes.CopyTo(nodes);

            for (int i = 0; i < localSize - 1; i++)
            {
                int nodeToInsert = nodes[i];

                for (int j = i + 1; j < localSize; j++)
                {
                    int posToInsert = nodes[j];

                    connectivityList[posToInsert].Add(nodeToInsert);
                }
            }
        }

        var orderedList = connectivityList.Select(list => list.OrderBy(val => val)).ToList();

        ig = new int[connectivityList.Count + 1];

        ig[0] = 0;
        ig[1] = 0;

        for (int i = 1; i < connectivityList.Count; i++)
        {
            ig[i + 1] = ig[i] + connectivityList[i].Count;
        }

        jg = new int[ig[^1]];

        for (int i = 1, j = 0; i < connectivityList.Count; i++)
        {
            foreach (var it in orderedList[i])
            {
                jg[j++] = it;
            }
        }
    }

    public static void PortraitByEdges(Mesh.Mesh mesh, out int[] ig, out int[] jg)
    {
        var elementsCount = mesh.Elements.Length;
        var edgesCount = mesh.Elements[^1].EdgesIndices[^1] + 1;

        var connectivityList = new List<HashSet<int>>();

        for (int i = 0; i < edgesCount; i++)
        {
            connectivityList.Add(new HashSet<int>());
        }

        for (int ielem = 0; ielem < elementsCount; ielem++)
        {
            var edges = mesh.Elements[ielem].EdgesIndices;

            for (int i = 0; i < 3; i++) {
                int iedge = edges[i];

                for (int j = i + 1; j < 4; j++) {
                    int jedge = edges[j];

                    connectivityList[jedge].Add(iedge);
                }
            }
        }

        var orderedList = connectivityList.Select(list => list.OrderBy(val => val)).ToList();
        
        ig = new int[edgesCount + 1];

        ig[0] = 0;
        ig[1] = 0;

        for (int i = 1; i < connectivityList.Count; i++) 
        {
            ig[i + 1] = ig[i] + connectivityList[i].Count;
        }

        jg = new int[ig[^1]];

        for (int i = 1, j = 0; i < orderedList.Count; i++) 
        {
            foreach (var it in orderedList[i])
            {
                jg[j++] = it;
            }
        }
    }
}