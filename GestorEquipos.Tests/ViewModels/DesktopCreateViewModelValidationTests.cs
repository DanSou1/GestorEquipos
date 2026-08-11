using System.ComponentModel.DataAnnotations;
using GestorEquipos.Models;
using GestorEquipos.Models.ViewModels.Desktop;
using Xunit;

namespace GestorEquipos.Tests.ViewModels
{
    public class DesktopCreateViewModelValidationTests
    {
        private static DesktopCreateViewModel BuildValidBase() => new()
        {
            NameDesktop = "PC-001",
            SerialNumber = "SN-001",
            Brand = "Dell",
            Model = "OptiPlex",
            Processor = "i5",
            Disk = "256GB SSD",
            OSVersionId = 1,
            RamId = 1
        };

        private static List<ValidationResult> Validate(DesktopCreateViewModel vm)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(vm, new ValidationContext(vm), results, validateAllProperties: true);
            return results;
        }

        [Fact]
        public void Validate_RemoteSelectionEmpty_HasNoRemoteRelatedErrors()
        {
            var vm = BuildValidBase();
            vm.RemoteSelection = "";

            var results = Validate(vm);

            Assert.Empty(results);
        }

        [Fact]
        public void Validate_RemoteSelectionNewWithoutConnectionType_ReturnsError()
        {
            var vm = BuildValidBase();
            vm.RemoteSelection = "new";

            var results = Validate(vm);

            Assert.Contains(results, r => r.MemberNames.Contains(nameof(DesktopCreateViewModel.NewRemoteConnectionType)));
        }

        [Fact]
        public void Validate_AplicativoWithoutAppDescription_ReturnsError()
        {
            var vm = BuildValidBase();
            vm.RemoteSelection = "new";
            vm.NewRemoteConnectionType = RemoteConnectionType.Aplicativo;

            var results = Validate(vm);

            Assert.Contains(results, r => r.MemberNames.Contains(nameof(DesktopCreateViewModel.NewRemoteAppDescription)));
        }

        [Fact]
        public void Validate_AplicativoWithAppDescription_IsValid()
        {
            var vm = BuildValidBase();
            vm.RemoteSelection = "new";
            vm.NewRemoteConnectionType = RemoteConnectionType.Aplicativo;
            vm.NewRemoteAppDescription = "SAP GUI";

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
            var vm = BuildValidBase();
            vm.RemoteSelection = "new";
            vm.NewRemoteConnectionType = RemoteConnectionType.EscritorioRemotoWindows;
            vm.NewRemoteIPAddress = ip;
            vm.NewRemotePort = port;
            vm.NewRemoteUsername = username;
            vm.NewRemotePassword = password;

            var results = Validate(vm);

            Assert.NotEmpty(results);
        }

        [Fact]
        public void Validate_RdpWithInvalidIPFormat_ReturnsError()
        {
            var vm = BuildValidBase();
            vm.RemoteSelection = "new";
            vm.NewRemoteConnectionType = RemoteConnectionType.EscritorioRemotoWindows;
            vm.NewRemoteIPAddress = "no-es-una-ip";
            vm.NewRemotePort = "3389";
            vm.NewRemoteUsername = "PC01\\jperez";
            vm.NewRemotePassword = "clave123";

            var results = Validate(vm);

            Assert.Contains(results, r => r.MemberNames.Contains(nameof(DesktopCreateViewModel.NewRemoteIPAddress)));
        }

        [Fact]
        public void Validate_RdpWithNonNumericPort_ReturnsError()
        {
            var vm = BuildValidBase();
            vm.RemoteSelection = "new";
            vm.NewRemoteConnectionType = RemoteConnectionType.EscritorioRemotoWindows;
            vm.NewRemoteIPAddress = "10.0.0.5";
            vm.NewRemotePort = "abc";
            vm.NewRemoteUsername = "PC01\\jperez";
            vm.NewRemotePassword = "clave123";

            var results = Validate(vm);

            Assert.Contains(results, r => r.MemberNames.Contains(nameof(DesktopCreateViewModel.NewRemotePort)));
        }

        [Fact]
        public void Validate_RdpWithAllFieldsValid_IsValid()
        {
            var vm = BuildValidBase();
            vm.RemoteSelection = "new";
            vm.NewRemoteConnectionType = RemoteConnectionType.EscritorioRemotoWindows;
            vm.NewRemoteIPAddress = "10.0.0.5";
            vm.NewRemotePort = "3389";
            vm.NewRemoteUsername = "PC01\\jperez";
            vm.NewRemotePassword = "clave123";

            var results = Validate(vm);

            Assert.Empty(results);
        }

        [Fact]
        public void Validate_RemoteSelectionExistingId_HasNoRemoteRelatedErrors()
        {
            var vm = BuildValidBase();
            vm.RemoteSelection = "7";

            var results = Validate(vm);

            Assert.Empty(results);
        }
    }
}
