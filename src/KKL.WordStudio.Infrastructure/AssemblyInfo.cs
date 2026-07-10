using System.Runtime.CompilerServices;

// Lets the test project unit-test the internal Word/* writer classes directly
// (Sprint 6) rather than only black-box through WordExporter's public API.
[assembly: InternalsVisibleTo("KKL.WordStudio.Infrastructure.Tests")]
