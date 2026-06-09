using System.Text;

namespace DcsMissionReader.Services.Interfaces
{
    public interface IBriefingStylesService
    {
        string BuildStylesKml();

        void AppendStyles(StringBuilder builder);
    }
}
