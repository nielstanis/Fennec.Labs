using System;

namespace FennecLabs.TestUtilities
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class TestProjectCsprojAttribute : Attribute
    {
        public TestProjectCsprojAttribute(string name, string path)
        {
            Name = name;
            Path = path;
        }

        public string Name { get; }
        public string Path { get; }
    }
}
