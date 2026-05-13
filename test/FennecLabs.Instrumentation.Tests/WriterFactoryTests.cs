using FennecLabs.Instrumentation.Output;

namespace FennecLabs.Instrumentation.Tests
{
    public class WriterFactoryTests
    {
        [Fact]
        public void CreateWriter_WithJsonFormat_ReturnsJsonWriter()
        {
            var writer = WriterFactory.CreateWriter(OutputFormat.Json, "/tmp");

            Assert.IsType<JsonWriter>(writer);
        }

        [Fact]
        public void CreateWriter_WithFxtFormat_ReturnsFxtWriter()
        {
            var writer = WriterFactory.CreateWriter(OutputFormat.Fxt, "/tmp");

            Assert.IsType<FxtWriter>(writer);
        }
    }
}
