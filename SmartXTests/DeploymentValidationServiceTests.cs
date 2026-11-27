using SmartX.API.Models;
using SmartX.API.Services;

namespace SmartX.Tests;

public class DeploymentValidationServiceTests
{
    [Fact]
    public void ValidateDeploymentTree_ShouldAcceptValidNestedTree()
    {
        // Arrange
        var service = new DeploymentValidationService();

        var root = new DeploymentNode
        {
            Name = "Building",
            Type = "Root",
            Children =
            {
                new DeploymentNode
                {
                    Name = "Floor-1",
                    Type = "Floor",
                    Children =
                    {
                        new DeploymentNode
                        {
                            Name = "Room-101",
                            Type = "Room"
                        }
                    }
                }
            }
        };

        // Act
        var result = service.ValidateDeploymentTree(root);

        // Assert
        Assert.True(result.Valid);
        Assert.Equal("Deployment tree is valid.", result.Message);
        Assert.Equal(3, result.NodesVisited);
        Assert.Equal(2, result.MaximumDepth);
    }

    [Fact]
    public void ValidateDeploymentTree_ShouldDetectDuplicateNode()
    {
        // Arrange
        var service = new DeploymentValidationService();

        var root = new DeploymentNode
        {
            Name = "Building",
            Type = "Root",
            Children =
            {
                new DeploymentNode
                {
                    Name = "Floor-1",
                    Type = "Floor"
                },
                new DeploymentNode
                {
                    Name = "Floor-1",
                    Type = "Floor"
                }
            }
        };

        // Act
        var result = service.ValidateDeploymentTree(root);

        // Assert
        Assert.False(result.Valid);
        Assert.Contains(
            "Duplicate or circular deployment node detected: Floor-1",
            result.Message);
    }

    [Fact]
    public void ValidateDeploymentTree_ShouldDetectInvalidNode()
    {
        // Arrange
        var service = new DeploymentValidationService();

        var root = new DeploymentNode
        {
            Name = "Building",
            Type = "Root",
            Children =
            {
                new DeploymentNode
                {
                    Name = "",
                    Type = "Floor"
                }
            }
        };

        // Act
        var result = service.ValidateDeploymentTree(root);

        // Assert
        Assert.False(result.Valid);
        Assert.Contains(
            "Invalid deployment node at depth 1",
            result.Message);
    }

    [Fact]
    public void ValidateDeploymentTree_ShouldCalculateMaximumDepth()
    {
        // Arrange
        var service = new DeploymentValidationService();

        var root = new DeploymentNode
        {
            Name = "Building",
            Type = "Root",
            Children =
            {
                new DeploymentNode
                {
                    Name = "Floor-1",
                    Type = "Floor",
                    Children =
                    {
                        new DeploymentNode
                        {
                            Name = "Room-101",
                            Type = "Room",
                            Children =
                            {
                                new DeploymentNode
                                {
                                    Name = "Sensor-A",
                                    Type = "Sensor"
                                }
                            }
                        }
                    }
                }
            }
        };

        // Act
        var result = service.ValidateDeploymentTree(root);

        // Assert
        Assert.True(result.Valid);
        Assert.Equal(4, result.NodesVisited);
        Assert.Equal(3, result.MaximumDepth);
    }
}