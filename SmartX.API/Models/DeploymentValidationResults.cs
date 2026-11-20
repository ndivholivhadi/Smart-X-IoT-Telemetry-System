namespace SmartX.API.Models
{
    public class DeploymentValidationResult
    {
        public bool Valid { get; set; }

        public string Message { get; set; } = string.Empty;

        public int NodesVisited { get; set; }

        public int MaximumDepth { get; set; }
    }
}