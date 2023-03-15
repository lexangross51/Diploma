namespace Diploma.Source.FEM;

public static class PortraitBuilder
{
    public static void PortraitByNodes(Mesh.Mesh mesh, out int[] ig, out int[] jg)
    {
        var connectivityList = new List<SortedSet<int>>();

        for (int i = 0; i < mesh.Points.Length; i++)
        {
            connectivityList.Add(new SortedSet<int>());
        }

        int localSize = mesh.Elements[0].Nodes.Length;
        
        foreach (var element in mesh.Elements)
        {
            for (int i = 0; i < localSize; i++)
            {
                int nodeToInsert = element.Nodes[i];

                for (int j = 0; j < localSize; j++)
                {
                    int posToInsert = element.Nodes[j];
                    
                    if (nodeToInsert < posToInsert)
                    {
                        connectivityList[posToInsert].Add(nodeToInsert);
                    }
                }
            }
        }

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
            foreach (var it in connectivityList[i])
            {
                jg[j++] = it;
            }
        }
    }

    public static void PortraitByEdges(Mesh.Mesh mesh, out int[] ig, out int[] jg)
    {
        var elementsCount = mesh.Elements.Length;
        var edgesCount = mesh.Elements[^1].EdgesIndices[^1] + 1;

        var connectivityList = new List<SortedSet<int>>();

        for (int i = 0; i < edgesCount; i++)
        {
            connectivityList.Add(new SortedSet<int>());
        }

        for (int ielem = 0; ielem < elementsCount; ielem++)
        {
            var edges = mesh.Elements[ielem].EdgesIndices;

            for (int i = 0; i < 4; i++) {
                int iedge = edges[i];

                for (int j = 0; j < 4; j++) {
                    int jedge = edges[j];

                    if (iedge < jedge)
                    {
                        connectivityList[jedge].Add(iedge);
                    }
                }
            }
        }

        ig = new int[edgesCount + 1];

        ig[0] = 0;
        ig[1] = 0;

        for (int i = 1; i < connectivityList.Count; i++) 
        {
            ig[i + 1] = ig[i] + connectivityList[i].Count;
        }

        jg = new int[ig[^1]];

        for (int i = 1, j = 0; i < connectivityList.Count; i++) 
        {
            foreach (var it in connectivityList[i])
            {
                jg[j++] = it;
            }
        }
    }
}