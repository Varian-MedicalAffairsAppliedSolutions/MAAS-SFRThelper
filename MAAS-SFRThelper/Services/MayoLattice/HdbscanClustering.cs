using System;
using System.Collections.Generic;
using System.Linq;

namespace MAAS_SFRThelper.Services.MayoLattice
{
    /// <summary>
    /// Result of HDBSCAN clustering.
    /// </summary>
    public class HdbscanResult
    {
        /// <summary>
        /// Cluster label for each input point. -1 indicates noise (unassigned).
        /// Labels are 0-indexed: 0, 1, 2, ..., NumClusters-1.
        /// </summary>
        public int[] Labels { get; set; }

        /// <summary>
        /// Number of clusters found (excluding noise).
        /// </summary>
        public int NumClusters { get; set; }
    }

    /// <summary>
    /// HDBSCAN (Hierarchical Density-Based Spatial Clustering of Applications with Noise).
    /// 
    /// Pure C# implementation following Campello, Moulavi, and Sander (2013).
    /// Designed for clustering 2D point sets on axial slices of the feasible
    /// lattice placement volume, as described in Deufel et al. 2024.
    /// 
    /// Algorithm steps:
    ///   1. Compute core distances (distance to k-th nearest neighbor)
    ///   2. Build mutual reachability distance matrix
    ///   3. Construct minimum spanning tree (Prim's algorithm)
    ///   4. Build single-linkage dendrogram from sorted MST edges
    ///   5. Condense the dendrogram (remove clusters smaller than MinClusterSize)
    ///   6. Extract final clusters via stability-based selection
    /// 
    /// Complexity: O(n^2) time and space, suitable for the per-slice point counts
    /// encountered in lattice placement (typically hundreds to low thousands).
    /// </summary>
    public class HdbscanClustering
    {
        private readonly int _minClusterSize;

        /// <summary>
        /// Create an HDBSCAN clustering instance.
        /// </summary>
        /// <param name="minClusterSize">
        /// Minimum number of points to form a cluster. Also used as
        /// the k parameter for core distance computation (minPts).
        /// Must be >= 2. Paper default: 5.
        /// </param>
        public HdbscanClustering(int minClusterSize = 5)
        {
            _minClusterSize = Math.Max(2, minClusterSize);
        }

        /// <summary>
        /// Run HDBSCAN clustering on a set of 2D points.
        /// </summary>
        /// <param name="points">
        /// List of 2D points, each as double[2] = {x, y}.
        /// </param>
        /// <returns>Clustering result with labels and cluster count.</returns>
        public HdbscanResult Run(List<double[]> points)
        {
            int n = points.Count;

            // Edge case: too few points to form any cluster
            if (n < _minClusterSize)
            {
                return new HdbscanResult
                {
                    Labels = Enumerable.Repeat(-1, n).ToArray(),
                    NumClusters = 0
                };
            }

            // Step 1: Compute pairwise Euclidean distances
            double[] distMatrix = ComputeDistanceMatrix(points, n);

            // Step 2: Compute core distances (distance to k-th nearest neighbor)
            double[] coreDistances = ComputeCoreDistances(distMatrix, n);

            // Step 3: Compute mutual reachability distances
            // Reuse the distance matrix storage — overwrite in place
            ApplyMutualReachability(distMatrix, coreDistances, n);

            // Step 4: Build MST using Prim's algorithm on mutual reachability graph
            var mstEdges = BuildMST(distMatrix, n);

            // Step 5: Sort MST edges by weight (ascending distance)
            mstEdges.Sort((a, b) => a.Weight.CompareTo(b.Weight));

            // Step 6: Build dendrogram by processing merges in order
            var dendrogram = BuildDendrogram(mstEdges, n);

            // Step 7: Condense the dendrogram
            var condensed = CondenseDendrogram(dendrogram, n);

            // Step 8: Extract clusters via stability selection
            int[] labels = ExtractClusters(condensed, n);

            // Relabel clusters to be 0-indexed contiguous
            labels = RelabelContiguous(labels);

            int numClusters = labels.Max() + 1;
            if (labels.All(l => l == -1)) numClusters = 0;

            return new HdbscanResult
            {
                Labels = labels,
                NumClusters = numClusters
            };
        }

        // ═══════════════════════════════════════════════════════════════
        // Step 1: Pairwise distance matrix (upper triangle, flat array)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Compute all pairwise Euclidean distances. Stored as a flat
        /// upper-triangular array: index for (i,j) where i &lt; j is
        /// i*n - i*(i+1)/2 + (j - i - 1).
        /// </summary>
        private double[] ComputeDistanceMatrix(List<double[]> points, int n)
        {
            int size = n * (n - 1) / 2;
            double[] dist = new double[size];

            for (int i = 0; i < n; i++)
            {
                double xi = points[i][0];
                double yi = points[i][1];

                for (int j = i + 1; j < n; j++)
                {
                    double dx = xi - points[j][0];
                    double dy = yi - points[j][1];
                    dist[TriIndex(i, j, n)] = Math.Sqrt(dx * dx + dy * dy);
                }
            }

            return dist;
        }

        /// <summary>
        /// Flat index into upper-triangular storage for pair (i, j) where i &lt; j.
        /// </summary>
        private int TriIndex(int i, int j, int n)
        {
            // Ensure i < j
            if (i > j) { int tmp = i; i = j; j = tmp; }
            return i * n - i * (i + 1) / 2 + (j - i - 1);
        }

        /// <summary>
        /// Get distance between points i and j from the flat upper-triangular array.
        /// </summary>
        private double GetDist(double[] distMatrix, int i, int j, int n)
        {
            if (i == j) return 0.0;
            return distMatrix[TriIndex(i, j, n)];
        }

        /// <summary>
        /// Set distance between points i and j in the flat upper-triangular array.
        /// </summary>
        private void SetDist(double[] distMatrix, int i, int j, int n, double value)
        {
            if (i == j) return;
            distMatrix[TriIndex(i, j, n)] = value;
        }

        // ═══════════════════════════════════════════════════════════════
        // Step 2: Core distances
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// For each point, compute the distance to its k-th nearest neighbor
        /// where k = MinClusterSize. This measures the local density around
        /// each point — points in sparse regions have large core distances.
        /// </summary>
        private double[] ComputeCoreDistances(double[] distMatrix, int n)
        {
            double[] coreDistances = new double[n];
            int k = _minClusterSize;

            for (int i = 0; i < n; i++)
            {
                // Collect all distances from point i to every other point
                double[] dists = new double[n - 1];
                int idx = 0;
                for (int j = 0; j < n; j++)
                {
                    if (j == i) continue;
                    dists[idx++] = GetDist(distMatrix, i, j, n);
                }

                // Sort and take the k-th smallest (0-indexed: k-1)
                Array.Sort(dists);
                int kIndex = Math.Min(k - 1, dists.Length - 1);
                coreDistances[i] = dists[kIndex];
            }

            return coreDistances;
        }

        // ═══════════════════════════════════════════════════════════════
        // Step 3: Mutual reachability distances
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Transform pairwise distances into mutual reachability distances.
        /// MRD(a, b) = max(core(a), core(b), dist(a, b)).
        /// 
        /// This inflates distances in sparse regions so that points in
        /// low-density areas are effectively pushed apart, making dense
        /// clusters more prominent.
        /// 
        /// Modifies distMatrix in place to save memory.
        /// </summary>
        private void ApplyMutualReachability(double[] distMatrix, double[] coreDistances, int n)
        {
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    int idx = TriIndex(i, j, n);
                    double d = distMatrix[idx];
                    double mrd = Math.Max(d, Math.Max(coreDistances[i], coreDistances[j]));
                    distMatrix[idx] = mrd;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Step 4: Minimum Spanning Tree (Prim's algorithm)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Build MST using Prim's algorithm on the mutual reachability graph.
        /// O(n^2) implementation using a simple nearest-neighbor scan.
        /// Returns n-1 edges.
        /// </summary>
        private List<MstEdge> BuildMST(double[] mrdMatrix, int n)
        {
            var edges = new List<MstEdge>(n - 1);
            bool[] inTree = new bool[n];
            double[] minWeight = new double[n]; // min edge weight connecting to tree
            int[] minFrom = new int[n];         // which tree node provides that min edge

            // Initialize: all nodes far away, start from node 0
            for (int i = 0; i < n; i++)
            {
                minWeight[i] = double.MaxValue;
                minFrom[i] = -1;
            }

            inTree[0] = true;

            // Update distances from node 0
            for (int j = 1; j < n; j++)
            {
                minWeight[j] = GetDist(mrdMatrix, 0, j, n);
                minFrom[j] = 0;
            }

            // Add n-1 edges
            for (int iter = 0; iter < n - 1; iter++)
            {
                // Find the non-tree node with smallest connecting weight
                int bestNode = -1;
                double bestWeight = double.MaxValue;

                for (int j = 0; j < n; j++)
                {
                    if (!inTree[j] && minWeight[j] < bestWeight)
                    {
                        bestWeight = minWeight[j];
                        bestNode = j;
                    }
                }

                if (bestNode == -1) break; // disconnected graph (shouldn't happen)

                // Add edge to MST
                edges.Add(new MstEdge
                {
                    From = minFrom[bestNode],
                    To = bestNode,
                    Weight = bestWeight
                });

                inTree[bestNode] = true;

                // Update distances for remaining non-tree nodes
                for (int j = 0; j < n; j++)
                {
                    if (!inTree[j])
                    {
                        double w = GetDist(mrdMatrix, bestNode, j, n);
                        if (w < minWeight[j])
                        {
                            minWeight[j] = w;
                            minFrom[j] = bestNode;
                        }
                    }
                }
            }

            return edges;
        }

        // ═══════════════════════════════════════════════════════════════
        // Step 5 & 6: Dendrogram construction
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Build a single-linkage dendrogram from sorted MST edges.
        /// Uses union-find to track component merges.
        /// 
        /// Returns an array of n-1 DendrogramNode entries.
        /// Leaf nodes are indices 0..n-1 (individual points).
        /// Internal nodes are indices n..2n-2.
        /// dendrogram[k] describes internal node (n + k).
        /// </summary>
        private DendrogramNode[] BuildDendrogram(List<MstEdge> sortedEdges, int n)
        {
            var uf = new UnionFind(n);
            var dendrogram = new DendrogramNode[n - 1];

            // Track which dendrogram node label each component root maps to.
            // Initially, each point is its own leaf node (label = point index).
            int[] componentLabel = new int[n];
            for (int i = 0; i < n; i++) componentLabel[i] = i;

            int nextLabel = n; // internal nodes start at index n

            for (int e = 0; e < sortedEdges.Count; e++)
            {
                var edge = sortedEdges[e];
                int rootA = uf.Find(edge.From);
                int rootB = uf.Find(edge.To);

                if (rootA == rootB) continue; // already in same component

                int labelA = componentLabel[rootA];
                int labelB = componentLabel[rootB];
                int sizeA = uf.Size(rootA);
                int sizeB = uf.Size(rootB);

                dendrogram[e] = new DendrogramNode
                {
                    Left = labelA,
                    Right = labelB,
                    Distance = edge.Weight,
                    Size = sizeA + sizeB
                };

                uf.Union(rootA, rootB);
                int newRoot = uf.Find(rootA);
                componentLabel[newRoot] = nextLabel;
                nextLabel++;
            }

            return dendrogram;
        }

        // ═══════════════════════════════════════════════════════════════
        // Step 7: Condense the dendrogram
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Condense the full dendrogram by collapsing splits where one child
        /// has fewer than MinClusterSize points. Small children become "noise"
        /// that falls out of the parent cluster. Only splits where BOTH
        /// children have >= MinClusterSize points are retained as genuine
        /// cluster births.
        /// 
        /// Returns a list of CondensedCluster objects forming a tree.
        /// </summary>
        private List<CondensedCluster> CondenseDendrogram(DendrogramNode[] dendrogram, int n)
        {
            var clusters = new List<CondensedCluster>();

            // Pre-compute the size of every node in the dendrogram.
            // Leaf nodes (< n) have size 1. Internal nodes have size from dendrogram.
            int totalNodes = 2 * n - 1;
            int[] nodeSize = new int[totalNodes];
            for (int i = 0; i < n; i++) nodeSize[i] = 1;
            for (int i = 0; i < dendrogram.Length; i++)
            {
                if (dendrogram[i].Size > 0) // valid merge entry
                    nodeSize[n + i] = dendrogram[i].Size;
            }

            // Find the root node (last valid merge)
            int rootIndex = -1;
            for (int i = dendrogram.Length - 1; i >= 0; i--)
            {
                if (dendrogram[i].Size > 0)
                {
                    rootIndex = n + i;
                    break;
                }
            }

            if (rootIndex == -1)
            {
                // No valid merges — treat all points as noise
                return clusters;
            }

            // Create root cluster (born at lambda = 0)
            int nextClusterId = 0;
            var rootCluster = new CondensedCluster
            {
                Id = nextClusterId++,
                ParentId = -1,
                LambdaBirth = 0.0,
                PointLambdas = new List<PointLambda>(),
                ChildClusterIds = new List<int>()
            };
            clusters.Add(rootCluster);

            // Recursive condensation
            CondenseNode(rootIndex, rootCluster.Id, dendrogram, nodeSize, n,
                         clusters, ref nextClusterId);

            // Compute lambda_death for each cluster.
            // A cluster "dies" when it splits into children, or at the maximum
            // lambda of its fallen-out points if it has no children.
            foreach (var cluster in clusters)
            {
                if (cluster.ChildClusterIds.Count > 0)
                {
                    // Died when it split — lambda_death = lambda_birth of children
                    double childBirth = clusters
                        .Where(c => cluster.ChildClusterIds.Contains(c.Id))
                        .Select(c => c.LambdaBirth)
                        .Max();
                    cluster.LambdaDeath = childBirth;
                }
                else if (cluster.PointLambdas.Count > 0)
                {
                    // Leaf cluster — dies at max lambda of its points
                    cluster.LambdaDeath = cluster.PointLambdas.Max(p => p.Lambda);
                }
                else
                {
                    cluster.LambdaDeath = cluster.LambdaBirth;
                }
            }

            // Compute stability for each cluster
            foreach (var cluster in clusters)
            {
                double stability = 0.0;
                foreach (var pl in cluster.PointLambdas)
                {
                    stability += (pl.Lambda - cluster.LambdaBirth);
                }
                cluster.Stability = stability;
            }

            return clusters;
        }

        /// <summary>
        /// Recursively condense a dendrogram node, assigning points to
        /// condensed clusters and identifying genuine splits.
        /// </summary>
        private void CondenseNode(int nodeId, int currentClusterId,
            DendrogramNode[] dendrogram, int[] nodeSize, int n,
            List<CondensedCluster> clusters, ref int nextClusterId)
        {
            // Base case: leaf node (single point).
            // In normal operation, single points (size 1 < minClusterSize) are always
            // caught as "too small" children by the parent node and collected via
            // CollectPoints. This base case is a safety net — if reached, record the
            // point to prevent silent data loss.
            if (nodeId < n)
            {
                clusters[currentClusterId].PointLambdas.Add(new PointLambda
                {
                    PointIndex = nodeId,
                    Lambda = double.MaxValue
                });
                return;
            }

            // Internal node
            int dendroIdx = nodeId - n;
            var node = dendrogram[dendroIdx];
            double lambda = (node.Distance > 0) ? (1.0 / node.Distance) : double.MaxValue;

            int leftId = node.Left;
            int rightId = node.Right;
            int leftSize = nodeSize[leftId];
            int rightSize = nodeSize[rightId];

            bool leftBig = leftSize >= _minClusterSize;
            bool rightBig = rightSize >= _minClusterSize;

            if (leftBig && rightBig)
            {
                // Genuine split: both children become new clusters
                var leftCluster = new CondensedCluster
                {
                    Id = nextClusterId++,
                    ParentId = currentClusterId,
                    LambdaBirth = lambda,
                    PointLambdas = new List<PointLambda>(),
                    ChildClusterIds = new List<int>()
                };
                var rightCluster = new CondensedCluster
                {
                    Id = nextClusterId++,
                    ParentId = currentClusterId,
                    LambdaBirth = lambda,
                    PointLambdas = new List<PointLambda>(),
                    ChildClusterIds = new List<int>()
                };

                clusters.Add(leftCluster);
                clusters.Add(rightCluster);
                clusters[currentClusterId].ChildClusterIds.Add(leftCluster.Id);
                clusters[currentClusterId].ChildClusterIds.Add(rightCluster.Id);

                // Points that stayed in the parent until this split get recorded
                // at lambda = this split's lambda (they "survive" to the split).
                // But actually, those points go INTO the child clusters, not recorded
                // as fallout from the parent. Only noise points are recorded.

                CondenseNode(leftId, leftCluster.Id, dendrogram, nodeSize, n,
                             clusters, ref nextClusterId);
                CondenseNode(rightId, rightCluster.Id, dendrogram, nodeSize, n,
                             clusters, ref nextClusterId);
            }
            else if (leftBig)
            {
                // Right is too small — its points fall out as noise from current cluster
                CollectPoints(rightId, currentClusterId, lambda, dendrogram, n, clusters);
                CondenseNode(leftId, currentClusterId, dendrogram, nodeSize, n,
                             clusters, ref nextClusterId);
            }
            else if (rightBig)
            {
                // Left is too small — its points fall out
                CollectPoints(leftId, currentClusterId, lambda, dendrogram, n, clusters);
                CondenseNode(rightId, currentClusterId, dendrogram, nodeSize, n,
                             clusters, ref nextClusterId);
            }
            else
            {
                // Both too small — all points fall out at this lambda
                CollectPoints(leftId, currentClusterId, lambda, dendrogram, n, clusters);
                CollectPoints(rightId, currentClusterId, lambda, dendrogram, n, clusters);
            }
        }

        /// <summary>
        /// Recursively collect all leaf points under a dendrogram node and
        /// record them as falling out of the given cluster at the specified lambda.
        /// </summary>
        private void CollectPoints(int nodeId, int clusterId, double lambda,
            DendrogramNode[] dendrogram, int n, List<CondensedCluster> clusters)
        {
            if (nodeId < n)
            {
                // Leaf: single point
                clusters[clusterId].PointLambdas.Add(new PointLambda
                {
                    PointIndex = nodeId,
                    Lambda = lambda
                });
                return;
            }

            // Internal node: recurse into both children
            int dendroIdx = nodeId - n;
            var node = dendrogram[dendroIdx];
            CollectPoints(node.Left, clusterId, lambda, dendrogram, n, clusters);
            CollectPoints(node.Right, clusterId, lambda, dendrogram, n, clusters);
        }

        // ═══════════════════════════════════════════════════════════════
        // Step 8: Cluster extraction via stability
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Select the most stable clusters using bottom-up traversal.
        /// 
        /// For each leaf cluster in the condensed tree: mark as selected.
        /// For each internal cluster (bottom-up): if its stability exceeds
        /// the combined stability of its selected descendants, select it
        /// instead (and deselect descendants).
        /// 
        /// Finally, assign each point to its selected cluster ancestor,
        /// or -1 for noise.
        /// </summary>
        private int[] ExtractClusters(List<CondensedCluster> condensed, int n)
        {
            int[] labels = Enumerable.Repeat(-1, n).ToArray();

            if (condensed.Count == 0) return labels;

            // Initialize: leaf clusters are selected
            foreach (var cluster in condensed)
            {
                cluster.Selected = (cluster.ChildClusterIds.Count == 0);
            }

            // Bottom-up stability propagation
            // Process clusters in reverse order (children before parents)
            // since cluster IDs are assigned depth-first, children always
            // have higher IDs than parents.
            for (int i = condensed.Count - 1; i >= 0; i--)
            {
                var cluster = condensed[i];
                if (cluster.ChildClusterIds.Count == 0) continue; // leaf, already handled

                // Sum selected stability of all descendants
                double childStabilitySum = 0;
                foreach (int childId in cluster.ChildClusterIds)
                {
                    childStabilitySum += GetSubtreeStability(condensed, childId);
                }

                if (cluster.Stability >= childStabilitySum)
                {
                    // This cluster is more stable than its children — select it
                    cluster.Selected = true;
                    DeselectDescendants(condensed, cluster.Id);
                }
                else
                {
                    // Children are better — propagate their stability up
                    cluster.Selected = false;
                    cluster.Stability = childStabilitySum;
                }
            }

            // Assign labels: each point gets the label of its nearest
            // selected ancestor cluster
            foreach (var cluster in condensed)
            {
                if (!cluster.Selected) continue;

                // All points that fell out within this cluster get this label
                foreach (var pl in cluster.PointLambdas)
                {
                    labels[pl.PointIndex] = cluster.Id;
                }

                // Points in descendant (non-selected) clusters also belong here
                AssignDescendantPoints(condensed, cluster, labels);
            }

            return labels;
        }

        /// <summary>
        /// Get the total stability of the selected subtree rooted at clusterId.
        /// </summary>
        private double GetSubtreeStability(List<CondensedCluster> condensed, int clusterId)
        {
            var cluster = condensed[clusterId];
            if (cluster.Selected) return cluster.Stability;

            double total = 0;
            foreach (int childId in cluster.ChildClusterIds)
            {
                total += GetSubtreeStability(condensed, childId);
            }
            return total;
        }

        /// <summary>
        /// Mark all descendant clusters as not selected.
        /// </summary>
        private void DeselectDescendants(List<CondensedCluster> condensed, int clusterId)
        {
            foreach (int childId in condensed[clusterId].ChildClusterIds)
            {
                condensed[childId].Selected = false;
                DeselectDescendants(condensed, childId);
            }
        }

        /// <summary>
        /// Assign labels to points in descendant clusters that are not selected.
        /// These points "belong" to the nearest selected ancestor.
        /// </summary>
        private void AssignDescendantPoints(List<CondensedCluster> condensed,
            CondensedCluster selectedCluster, int[] labels)
        {
            foreach (int childId in selectedCluster.ChildClusterIds)
            {
                var child = condensed[childId];
                // Child is not selected (its parent was chosen instead)
                foreach (var pl in child.PointLambdas)
                {
                    labels[pl.PointIndex] = selectedCluster.Id;
                }
                // Recurse into grandchildren
                AssignDescendantPoints(condensed, child, labels);
            }
        }

        /// <summary>
        /// Relabel cluster IDs to be contiguous 0-indexed integers.
        /// Noise points (-1) remain -1.
        /// </summary>
        private int[] RelabelContiguous(int[] labels)
        {
            var uniqueLabels = labels.Where(l => l >= 0).Distinct().OrderBy(l => l).ToList();

            if (uniqueLabels.Count == 0) return labels;

            var mapping = new Dictionary<int, int>();
            for (int i = 0; i < uniqueLabels.Count; i++)
            {
                mapping[uniqueLabels[i]] = i;
            }

            int[] relabeled = new int[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                relabeled[i] = labels[i] < 0 ? -1 : mapping[labels[i]];
            }

            return relabeled;
        }

        // ═══════════════════════════════════════════════════════════════
        // Internal data structures
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Edge in the minimum spanning tree.
        /// </summary>
        private struct MstEdge
        {
            public int From;
            public int To;
            public double Weight;
        }

        /// <summary>
        /// Node in the single-linkage dendrogram.
        /// Represents the merge of two sub-trees at a given distance.
        /// </summary>
        private struct DendrogramNode
        {
            /// <summary>Index of the left child (0..n-1 for leaves, n+ for internal).</summary>
            public int Left;
            /// <summary>Index of the right child.</summary>
            public int Right;
            /// <summary>Mutual reachability distance at which this merge occurred.</summary>
            public double Distance;
            /// <summary>Total number of points in the merged subtree.</summary>
            public int Size;
        }

        /// <summary>
        /// A cluster in the condensed dendrogram tree.
        /// </summary>
        private class CondensedCluster
        {
            /// <summary>Unique cluster ID (0-indexed).</summary>
            public int Id;
            /// <summary>Parent cluster ID (-1 for root).</summary>
            public int ParentId;
            /// <summary>Lambda (1/distance) at which this cluster was born.</summary>
            public double LambdaBirth;
            /// <summary>Lambda at which this cluster died (split or exhausted).</summary>
            public double LambdaDeath;
            /// <summary>
            /// Points that fall out of this cluster (noise at various lambdas),
            /// plus points that survive to the cluster's death.
            /// </summary>
            public List<PointLambda> PointLambdas;
            /// <summary>IDs of immediate child clusters (from genuine splits).</summary>
            public List<int> ChildClusterIds;
            /// <summary>Cluster stability score.</summary>
            public double Stability;
            /// <summary>Whether this cluster is selected in the final extraction.</summary>
            public bool Selected;
        }

        /// <summary>
        /// Records when a point falls out of (or is associated with) a cluster.
        /// </summary>
        private struct PointLambda
        {
            /// <summary>Index of the point in the original input array.</summary>
            public int PointIndex;
            /// <summary>Lambda value (1/distance) at which the point event occurred.</summary>
            public double Lambda;
        }

        // ═══════════════════════════════════════════════════════════════
        // Union-Find (Disjoint Set) with path compression and union by rank
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Union-Find data structure for efficient component tracking
        /// during dendrogram construction.
        /// </summary>
        private class UnionFind
        {
            private int[] _parent;
            private int[] _rank;
            private int[] _size;

            public UnionFind(int n)
            {
                _parent = new int[n];
                _rank = new int[n];
                _size = new int[n];
                for (int i = 0; i < n; i++)
                {
                    _parent[i] = i;
                    _rank[i] = 0;
                    _size[i] = 1;
                }
            }

            /// <summary>
            /// Find the root representative of the set containing x.
            /// Uses path compression.
            /// </summary>
            public int Find(int x)
            {
                if (_parent[x] != x)
                    _parent[x] = Find(_parent[x]);
                return _parent[x];
            }

            /// <summary>
            /// Merge the sets containing x and y.
            /// Uses union by rank.
            /// </summary>
            public void Union(int x, int y)
            {
                int rx = Find(x);
                int ry = Find(y);
                if (rx == ry) return;

                if (_rank[rx] < _rank[ry])
                {
                    _parent[rx] = ry;
                    _size[ry] += _size[rx];
                }
                else if (_rank[rx] > _rank[ry])
                {
                    _parent[ry] = rx;
                    _size[rx] += _size[ry];
                }
                else
                {
                    _parent[ry] = rx;
                    _size[rx] += _size[ry];
                    _rank[rx]++;
                }
            }

            /// <summary>
            /// Get the size of the set containing x.
            /// </summary>
            public int Size(int x)
            {
                return _size[Find(x)];
            }
        }
    }
}