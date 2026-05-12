using FennecLabs.Instrumentation.Output;

namespace FennecLabs.Instrumentation.Tests
{
    public class WriterFactoryTests
    {
        [Fact]
        public void CreateWriter_WithJsonLowercase_ReturnsJsonWriter()
        {
            var writer = WriterFactory.CreateWriter("json", "/tmp");

            Assert.IsType<JsonWriter>(writer);
        }

        [Fact]
        public void CreateWriter_WithJsonMixedCase_ReturnsJsonWriter()
        {
            var writer = WriterFactory.CreateWriter("JSON", "/tmp");

            Assert.IsType<JsonWriter>(writer);
        }

        [Fact]
        public void CreateWriter_WithFxt_ReturnsFxtWriter()
        {
            var writer = WriterFactory.CreateWriter("fxt", "/tmp");

            Assert.IsType<FxtWriter>(writer);
        }

        [Fact]
        public void CreateWriter_WithUnknownType_ReturnsFxtWriter()
        {
            var writer = WriterFactory.CreateWriter("sarif", "/tmp");

            Assert.IsType<FxtWriter>(writer);
        }
    }
}
