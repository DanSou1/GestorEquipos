using System.ComponentModel.DataAnnotations;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Remote;
using Xunit;

namespace GestorEquipos.Tests.ViewModels
{
    public class RemoteEditViewModelValidationTests
    {
        private static List<ValidationResult> Validate(RemoteEditViewModel vm)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(vm, new ValidationContext(vm), results, validateAllProperties: true);
            return results;
        }

        [Fact]
        public void Validate_AplicativoWithoutAppDescription_ReturnsError()
        {
            var vm = new RemoteEditViewModel { ConnectionType = RemoteConnectionType.Aplicativo };

            var results = Validate(vm);

            Assert.Contains(results, r => r.MemberNames.Contains(nameof(RemoteEditViewModel.AppDescription)));
        }

        [Fact]
        public void Validate_AplicativoWithAppDescription_IsValid()
        {
            var vm = new RemoteEditViewModel { ConnectionType = RemoteConnectionType.Aplicativo, AppDescription = "SAP GUI" };

            var results = Validate(vm);

            Assert.Empty(results);
        }

        [Theory]
        [InlineData(null, "3389", "PC01\\jperez", "clave123")]
        [InlineData("10.0.0.5", null, "PC01\\jperez", "clave123")]
        [InlineData("10.0.0.5", "3389", null, "clave123")]
        [InlineData("10.0.0.5", "3389", "PC01\\jperez", null)]
        public void Validate_RdpMissingAnyField_ReturnsError(string? ip, string? port, string? username, string? password)
        {
            var vm = new RemoteEditViewModel
            {
                ConnectionType = RemoteConnectionType.EscritorioRemotoWindows,
                IPAddress = ip,
                Port = port,
                Username = username,
                Password = password
            };

            var results = Validate(vm);

            Assert.NotEmpty(results);
        }

        [Fact]
        public void Validate_RdpWithInvalidIPFormat_ReturnsError()
        {
            var vm = new RemoteEditViewModel
            {
                ConnectionType = RemoteConnectionType.EscritorioRemotoWindows,
                IPAddress = "no-es-una-ip",
                Port = "3389",
                Username = "PC01\\jperez",
                Password = "clave123"
            };

            var results = Validate(vm);

            Assert.Contains(results, r => r.MemberNames.Contains(nameof(RemoteEditViewModel.IPAddress)));
        }

        [Fact]
        public void Validate_RdpWithNonNumericPort_ReturnsError()
        {
            var vm = new RemoteEditViewModel
            {
                ConnectionType = RemoteConnectionType.EscritorioRemotoWindows,
                IPAddress = "10.0.0.5",
                Port = "abc",
                Username = "PC01\\jperez",
                Password = "clave123"
            };

            var results = Validate(vm);

            Assert.Contains(results, r => r.MemberNames.Contains(nameof(RemoteEditViewModel.Port)));
        }

        [Fact]
        public void Validate_RdpWithAllFieldsValid_IsValid()
        {
            var vm = new RemoteEditViewModel
            {
                ConnectionType = RemoteConnectionType.EscritorioRemotoWindows,
                IPAddress = "10.0.0.5",
                Port = "3389",
                Username = "PC01\\jperez",
                Password = "clave123"
            };

            var results = Validate(vm);

            Assert.Empty(results);
        }
    }
}
