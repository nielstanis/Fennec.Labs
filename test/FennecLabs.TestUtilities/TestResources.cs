using System.Linq;
using System.Reflection;

namespace FennecLabs.TestUtilities
{
    public class TestResources
    {
        public static string GetTestProjectAssembly(string name)
        {
            var callingAssembly = Assembly.GetCallingAssembly();
            return callingAssembly
                .GetCustomAttributes<TestProjectReferenceAttribute>()
                .First(a => a.Name == name)
                .Path;
        }

        public static string GetTestProjectCsprojPath(string name)
        {
            var callingAssembly = Assembly.GetCallingAssembly();
            return callingAssembly
                .GetCustomAttributes<TestProjectCsprojAttribute>()
                .First(a => a.Name == name)
                .Path;
        }
    }
}

