using System.Linq;
using System.Reflection;

namespace FennecLabs.TestUtilities
{
    public class TestResources
    {
        public static string GetTestProjectAssembly(string name)
        {
            // Look in the calling assembly (the test assembly) for the attribute
            var callingAssembly = Assembly.GetCallingAssembly();
            return callingAssembly
                .GetCustomAttributes<TestProjectReferenceAttribute>()
                .First(a => a.Name == name)
                .Path;
        }
    }
}

