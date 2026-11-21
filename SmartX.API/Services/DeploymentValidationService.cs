using SmartX.API.Models;

namespace SmartX.API.Services
{
    public class DeploymentValidationService
    {
        public DeploymentValidationResult ValidateDeploymentTree(DeploymentNode node,
            HashSet<string>? visitedNodes = null,
            int depth = 0)
        {
            visitedNodes ??= new HashSet<string>();

            if (node == null)
            {
                return new DeploymentValidationResult
                {
                    Valid = false,
                    Message = "Deployment node cannot be null."
                };
            }

            if (string.IsNullOrWhiteSpace(node.Name) || string.IsNullOrWhiteSpace(node.Type))
            {
                return new DeploymentValidationResult
                {
                    Valid = false,
                    Message = $"Invalid deployment node at depth {depth}."
                };
            }

            if (!visitedNodes.Add(node.Name))
            {
                return new DeploymentValidationResult
                {
                    Valid = false,
                    Message =
                        $"Duplicate or circular deployment node detected: {node.Name}"
                };
            }

            int nodesVisited = 1;
            int maximumDepth = depth;

            if (node.Children == null || node.Children.Count == 0)
            {
                return new DeploymentValidationResult
                {
                    Valid = true,
                    Message = "Deployment tree is valid.",
                    NodesVisited = nodesVisited,
                    MaximumDepth = maximumDepth
                };
            }

            foreach (var child in node.Children)
            {
                var childResult = ValidateDeploymentTree(
                    child,
                    visitedNodes,
                    depth + 1);

                if (!childResult.Valid)
                {
                    return childResult;
                }

                nodesVisited += childResult.NodesVisited;
                maximumDepth = Math.Max(
                    maximumDepth,
                    childResult.MaximumDepth);
            }

            return new DeploymentValidationResult
            {
                Valid = true,
                Message = "Deployment tree is valid.",
                NodesVisited = nodesVisited,
                MaximumDepth = maximumDepth
            };
        }
    }
}