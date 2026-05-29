using DcsMissionReader;
using MoonSharp.Interpreter;

namespace DcsMissionReaderTests
{
    public class MissionUtilsTests
    {
        [Fact]
        public void TableToObject_ParsesNestedLuaTable_ReturnsCSharpDictionary()
        {
            // Arrange
            var script = new Script();
            string lua = @"
            return {
                strKey = 'value',
                numKey = 42,
                boolKey = true,
                nestedTable = {
                    [1] = 'arrayItem1',
                    [2] = 'arrayItem2'
                }
            }";

            // FIX: Keep it as a DynValue, do NOT append .Table
            var dynValue = script.DoString(lua);

            // Act
            // FIX: Pass the DynValue directly
            var result = MissionUtils.TableToObject(dynValue) as Dictionary<string, object>;

            // Assert
            Assert.NotNull(result);
            Assert.Equal("value", result["strKey"]);
            Assert.Equal(42.0, result["numKey"]);
            Assert.Equal(true, result["boolKey"]);

            var nested = result["nestedTable"] as Dictionary<string, object>;
            Assert.NotNull(nested);

            Assert.True(nested.ContainsKey("1") || nested.ContainsKey("1.0"));
        }

        [Fact]
        public void Resolve_WithDictKey_ReturnsDictionaryValue()
        {
            // Arrange
            var script = new Script();
            var missionVal = DynValue.NewString("DictKey_Description_123");
            var dictTable = script.DoString("return { DictKey_Description_123 = 'Actual Briefing Text' }").Table;

            // Act
            var result = MissionUtils.Resolve(missionVal, dictTable);

            // Assert
            Assert.Equal("Actual Briefing Text", result);
        }

        [Fact]
        public void Resolve_WithoutDictKey_ReturnsRawString()
        {
            // Arrange
            var missionVal = DynValue.NewString("Normal Text");

            // Act
            var result = MissionUtils.Resolve(missionVal, null);

            // Assert
            Assert.Equal("Normal Text", result);
        }

        [Fact]
        public void SanitizeFileName_RemovesInvalidCharacters()
        {
            // Arrange
            string invalidName = "My:Awesome/Mission?Name.miz";

            // Act
            string result = MissionUtils.SanitizeFileName(invalidName);

            // Assert
            Assert.DoesNotContain(":", result);
            Assert.DoesNotContain("/", result);
            Assert.DoesNotContain("?", result);
        }
    }
}