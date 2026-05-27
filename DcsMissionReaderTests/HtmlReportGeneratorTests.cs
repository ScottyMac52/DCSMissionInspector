using DcsMissionReader.Models;
using DcsMissionReader.Services.Generators;
using DcsMissionReader.Services.Interfaces;
using MoonSharp.Interpreter;
using Moq;

namespace DcsMissionReaderTests
{
    public class HtmlReportGeneratorTests
    {
        private readonly Mock<IFileManagementService> _fileMock = new();
        private readonly HtmlReportGenerator _generator;

        public HtmlReportGeneratorTests()
        {
            _generator = new HtmlReportGenerator(_fileMock.Object);
        }

        [Fact]
        public void Export_ShouldCreateDirectoriesAndCopyFiles()
        {
            // Arrange
            var script = new Script();
            var missionTable = new Table(script);

            // Add required metadata keys that cause the NullReference
            missionTable.Set("date", DynValue.NewTable(new Table(script)));
            missionTable.Get("date").Table.Set("Year", DynValue.NewNumber(2026));
            missionTable.Get("date").Table.Set("Month", DynValue.NewNumber(5));
            missionTable.Get("date").Table.Set("Day", DynValue.NewNumber(26));
            missionTable.Set("start_time", DynValue.NewNumber(36000));
            missionTable.Set("version", DynValue.NewString("1.0"));

            var context = new MissionContext
            {
                ReportDir = "test",
                TempDir = "temp",
                Sortie = "TestSortie",
                MissionTable = missionTable,
                Options = new AppOptions() { CreateHtml = true }
            };

            // Act
            _generator.Export(context);

            // Assert
            _fileMock.Verify(m => m.CopyImages("temp", It.IsAny<string>()), Times.Once);
            Assert.True(Directory.Exists(Path.Combine("test", "images")));

            // Cleanup
            if (Directory.Exists("test")) Directory.Delete("test", true);
        }

        [Fact]
        public void ParseWaypoints_ShouldExtractCorrectData_FromTable()
        {
            // Arrange: Create a minimal Lua table structure
            var script = new Script();
            var mission = new Table(script);
            var points = new Table(script);
            var wp1 = new Table(script);
            wp1.Set("x", DynValue.NewNumber(100));
            wp1.Set("y", DynValue.NewNumber(200));
            points.Set(1, DynValue.NewTable(wp1));

            // Act
            var results = HtmlReportGenerator.ParseWaypoints(points, mission);

            // Assert
            Assert.Single(results);
            Assert.Equal(100, results[0].x);
            Assert.Equal(200, results[0].y);
        }

        [Fact]
        public void ExtractPlayerSlots_ParsesMissionTable_ReturnsOnlyClientAndPlayerSlots()
        {
            // Arrange
            var script = new Script();

            string luaMission = @"
            return {
                coalition = {
                    blue = {
                        country = {
                            [1] = {
                                plane = {
                                    group = {
                                        [1] = {
                                            name = 'Hawg 1',
                                            task = 'CAS',
                                            units = {
                                                [1] = { type = 'A-10C_2', skill = 'Client' },
                                                [2] = { type = 'A-10C_2', skill = 'Client' }
                                            }
                                        }
                                    }
                                },
                                helicopter = {
                                    group = {
                                        [1] = {
                                            name = 'Chevy 1',
                                            task = 'Transport',
                                            units = {
                                                [1] = { type = 'UH-1H', skill = 'High' }, -- AI, should be ignored
                                                [2] = { type = 'UH-1H', playerCanDrive = true } -- Combined Arms client
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    red = { country = {} },
                    neutral = { country = {} }
                }
            }";

            var missionTable = script.DoString(luaMission).Table;

            // Act
            var result = HtmlReportGenerator.ExtractPlayerSlots(missionTable);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);

            // Verify A-10C Client Group
            var hawgGroup = result.Single(g => g.GroupName == "Hawg 1");
            Assert.Equal("blue", hawgGroup.Coalition);
            Assert.Equal("A-10C_2", hawgGroup.AircraftType);
            Assert.Equal("CAS", hawgGroup.Task);
            Assert.Equal(2, hawgGroup.ClientCount); // Counted both client slots

            // Verify UH-1H PlayerCanDrive Group
            var chevyGroup = result.Single(g => g.GroupName == "Chevy 1");
            Assert.Equal(1, chevyGroup.ClientCount); // Ignored the AI 'High' skill unit, only counted the playerCanDrive unit
        }

        [Fact]
        public void ExtractUnitsAndTargets_ParsesMissionTable_AggregatesGroundAndSeaUnits()
        {
            // Arrange
            var script = new Script();

            string luaMission = @"
            return {
                coalition = {
                    red = {
                        country = {
                            [1] = {
                                vehicle = {
                                    group = {
                                        [1] = {
                                            name = 'Armor Platoon',
                                            x = 5000.0,
                                            y = 6000.0,
                                            units = {
                                                [1] = { type = 'T-90' },
                                                [2] = { type = 'T-90' },
                                                [3] = { type = 'BMP-3' }
                                            }
                                        }
                                    }
                                },
                                ship = {
                                    group = {
                                        [1] = {
                                            name = 'Russian Cruiser',
                                            x = 1000.0,
                                            y = 2000.0,
                                            units = {
                                                [1] = { type = 'Moskva' }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    blue = { country = {} },
                    neutral = { country = {} }
                }
            }";

            var missionTable = script.DoString(luaMission).Table;

            // Act
            var result = HtmlReportGenerator.ExtractUnitsAndTargets(missionTable);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);

            // Verify Vehicle Aggregation
            var armorGroup = result.Single(g => g.GroupName == "Armor Platoon");
            Assert.Equal("red", armorGroup.Coalition);
            Assert.Equal("vehicle", armorGroup.Category);
            Assert.Equal(5000.0, armorGroup.X);
            Assert.Equal(3, armorGroup.TotalUnits);
            Assert.Contains("T-90 ×2", armorGroup.UnitInfo);
            Assert.Contains("BMP-3 ×1", armorGroup.UnitInfo);

            // Verify Ship Aggregation
            var shipGroup = result.Single(g => g.GroupName == "Russian Cruiser");
            Assert.Equal("ship", shipGroup.Category);
            Assert.Equal(1, shipGroup.TotalUnits);
            Assert.Contains("Moskva ×1", shipGroup.UnitInfo);
        }

        [Fact]
        public void ExtractOrderOfBattle_ParsesMissionTable_AggregatesTotalsAndAircraftTypes()
        {
            // Arrange
            var script = new Script();

            string luaMission = @"
            return {
                coalition = {
                    blue = {
                        country = {
                            [1] = {
                                plane = {
                                    group = {
                                        [1] = { units = { [1] = { type = 'F-16C_50' }, [2] = { type = 'F-16C_50' } } },
                                        [2] = { units = { [1] = { type = 'F/A-18C_hornet' } } }
                                    }
                                },
                                helicopter = {
                                    group = {
                                        [1] = { units = { [1] = { type = 'AH-64D_BLK_II' }, [2] = { type = 'AH-64D_BLK_II' } } }
                                    }
                                },
                                vehicle = {
                                    group = {
                                        [1] = { units = { [1] = { type = 'M-1 Abrams' }, [2] = { type = 'M-1 Abrams' } } },
                                        [2] = { units = { [1] = { type = 'M2A2 Bradley' } } }
                                    }
                                },
                                ship = {
                                    group = {
                                        [1] = { units = { [1] = { type = 'CVN_73' } } }
                                    }
                                }
                            }
                        }
                    },
                    red = { country = {} },
                    neutral = { country = {} }
                }
            }";

            var missionTable = script.DoString(luaMission).Table;

            // Act
            var result = HtmlReportGenerator.ExtractOrderOfBattle(missionTable);

            // Assert
            Assert.NotNull(result);

            var blueSide = result.FirstOrDefault(r => r.Coalition == "blue");
            Assert.NotNull(blueSide);

            // Aircraft totals (planes + helos counted individually)
            Assert.Equal(5, blueSide.TotalAircraft);
            Assert.Equal(2, blueSide.AircraftBreakdown["F-16C_50"]);
            Assert.Equal(1, blueSide.AircraftBreakdown["F/A-18C_hornet"]);
            Assert.Equal(2, blueSide.AircraftBreakdown["AH-64D_BLK_II"]);

            // Ship, Vehicle, and Static totals (counted by groups, NOT individual units)
            Assert.Equal(1, blueSide.TotalShips);
            Assert.Equal(2, blueSide.TotalGround); // 2 Vehicle groups
            Assert.Equal(0, blueSide.TotalStatics);
        }

        [Fact]
        public void ExtractAtoData_ParsesMissionTable_ExtractsGroupTasksAndTimes()
        {
            // Arrange
            var script = new Script();

            string luaMission = @"
            return {
                coalition = {
                    blue = {
                        country = {
                            [1] = {
                                plane = {
                                    group = {
                                        [1] = {
                                            name = 'Enfield 1',
                                            task = 'CAP',
                                            start_time = 36000, -- 10:00:00
                                            units = {
                                                [1] = { type = 'F-15C' },
                                                [2] = { type = 'F-15C' }
                                            }
                                        }
                                    }
                                },
                                ship = {
                                    group = {
                                        [1] = {
                                            name = 'CVN-74 John C. Stennis',
                                            task = 'Nothing',
                                            start_time = 0,
                                            units = {
                                                [1] = { type = 'CVN_74' }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    red = { country = {} },
                    neutral = { country = {} }
                }
            }";

            var missionTable = script.DoString(luaMission).Table;

            // Act
            var result = HtmlReportGenerator.ExtractAtoData(missionTable);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);

            var enfield = result.Single(r => r.GroupName == "Enfield 1");
            Assert.Equal("blue", enfield.Coalition);
            Assert.Equal("CAP", enfield.Task);
            Assert.Equal("F-15C", enfield.AircraftType);
            Assert.Equal(2, enfield.UnitsCount);
            Assert.Equal(36000, enfield.StartTimeSec);

            var stennis = result.Single(r => r.GroupName == "CVN-74 John C. Stennis");
            Assert.Equal("Nothing", stennis.Task);
            Assert.Equal("CVN_74", stennis.AircraftType);
            Assert.Equal(1, stennis.UnitsCount);
            Assert.Equal(0, stennis.StartTimeSec);
        }
    }
}
