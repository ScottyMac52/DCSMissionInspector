using Xunit;
using DcsMissionReader.Services;
using MoonSharp.Interpreter;

namespace DcsMissionReader.Tests.Services
{
    public class MissionIndexerTests
    {
        [Fact]
        public void BuildIndex_ParsesMissionTable_PopulatesDictionariesAndLocations()
        {
            // Arrange
            var script = new Script();
            string luaMission = @"
            return {
                coalition = {
                    blue = {
                        country = {
                            [1] = {
                                vehicle = {
                                    group = {
                                        [1] = {
                                            name = 'Alpha Armor',
                                            groupId = 105,
                                            units = {
                                                [1] = { type = 'M-1 Abrams', x = 5000, y = 6000 }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    red = {
                        country = {
                            [1] = {
                                plane = {
                                    group = {
                                        [1] = {
                                            name = 'Mig CAP',
                                            groupId = 201,
                                            units = {
                                                [1] = { type = 'MiG-29S', x = -1000, y = -2000 }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    neutral = { country = {} }
                }
            }";

            var missionTable = script.DoString(luaMission).Table;

            // Act
            var indexer = new MissionIndexer(missionTable);

            // Assert
            Assert.Equal(2, indexer.GroupsById.Count);
            Assert.Equal(2, indexer.GroupsByName.Count);
            Assert.Equal(2, indexer.UnitLocations.Count);

            // Verify O(1) Lookups
            Assert.Equal("Alpha Armor", indexer.ResolveNameFromGroupId(105));
            Assert.Equal("Mig CAP", indexer.ResolveNameFromGroupId(201));
            Assert.Equal("Unknown Target", indexer.ResolveNameFromGroupId(999)); // Invalid ID

            // Verify Coordinate Lookups (within 10m tolerance)
            Assert.Equal("M-1 Abrams", indexer.FindUnitTypeAtLocation(5005, 6005));
            Assert.Equal("MiG-29S", indexer.FindUnitTypeAtLocation(-1000, -2000));
            Assert.Null(indexer.FindUnitTypeAtLocation(0, 0));
        }
    }
}