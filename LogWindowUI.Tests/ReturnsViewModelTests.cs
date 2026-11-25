using System;
using System.Collections.Generic;
using System.Net.Http;
using GoatStore.Core.DTOs;
using GoatStore.DataAccess.Entities;
using LogWindowUI.ViewModels;
using Xunit;

namespace LogWindowUI.Tests
{
    public class ReturnsViewModelTests
    {
        private readonly FakeSaleApiClient _saleApi = new();
        private readonly FakeDialogService _dialog = new();

        private ReturnsViewModel CreateViewModel() => new(_saleApi, _dialog);

        private static SaleDto SampleSale(int number = 15, decimal refundedTotal = 0m, int returnedFirst = 0) => new()
        {
            Id = Guid.NewGuid(),
            Number = number,
            PaymentMethod = PaymentMethod.Card,
            Subtotal = 2500m,
            Discount = 100m,
            Total = 2400m,
            RefundedTotal = refundedTotal,
            CreatedAt = DateTime.UtcNow,
            Items = new List<SaleItemDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ProductId = Guid.NewGuid(),
                    Title = "Шкарпетки Nike",
                    Sku = "GST-SK01",
                    Quantity = 3,
                    ReturnedQuantity = returnedFirst,
                    UnitPrice = 500m
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ProductId = Guid.NewGuid(),
                    Title = "Худі Oversize",
                    Sku = "GST-HD01",
                    Quantity = 1,
                    ReturnedQuantity = 0,
                    UnitPrice = 1000m
                }
            }
        };

        private ReturnsViewModel LoadSampleSale(int returnedFirst = 0)
        {
            var vm = CreateViewModel();
            _saleApi.GetResponse = new SaleResponseDto
            {
                IsSuccess = true,
                Sale = SampleSale(15, 0m, returnedFirst)
            };
            vm.ReceiptNumberInput = "15";
            vm.FindSaleCommand.Execute(null);
            return vm;
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("abc")]
        [InlineData("0")]
        [InlineData("-5")]
        public void FindSaleCommand_InvalidNumber_ShowsWarning(string input)
        {
            var vm = CreateViewModel();
            vm.ReceiptNumberInput = input;

            vm.FindSaleCommand.Execute(null);

            Assert.Equal("Введіть коректний номер чека!", _dialog.LastMessage);
        }

        [Fact]
        public void FindSaleCommand_InvalidNumber_DoesNotCallApi()
        {
            var vm = CreateViewModel();
            vm.ReceiptNumberInput = "abc";

            vm.FindSaleCommand.Execute(null);

            Assert.Null(_saleApi.LastRequestedNumber);
        }

        [Fact]
        public void FindSaleCommand_NumberWithHashPrefix_ParsesNumber()
        {
            var vm = CreateViewModel();
            _saleApi.GetResponse = new SaleResponseDto { IsSuccess = true, Sale = SampleSale(42) };
            vm.ReceiptNumberInput = " #42 ";

            vm.FindSaleCommand.Execute(null);

            Assert.Equal(42, _saleApi.LastRequestedNumber);
        }

        [Fact]
        public void FindSaleCommand_Success_SetsHasSaleTrue()
        {
            var vm = LoadSampleSale();

            Assert.True(vm.HasSale);
        }

        [Fact]
        public void FindSaleCommand_Success_PopulatesItems()
        {
            var vm = LoadSampleSale();

            Assert.Equal(2, vm.Items.Count);
        }

        [Fact]
        public void FindSaleCommand_PartiallyReturnedItem_CalculatesAvailableQuantity()
        {
            var vm = LoadSampleSale(returnedFirst: 1);

            Assert.Equal(2, vm.Items[0].AvailableToReturn);
        }

        [Fact]
        public void FindSaleCommand_NotFound_ShowsServerMessage()
        {
            var vm = CreateViewModel();
            _saleApi.GetResponse = new SaleResponseDto { IsSuccess = false, Message = "Чек не знайдено." };
            vm.ReceiptNumberInput = "999";

            vm.FindSaleCommand.Execute(null);

            Assert.Equal("Чек не знайдено.", _dialog.LastMessage);
        }

        [Fact]
        public void FindSaleCommand_NotFound_ClearsPreviousItems()
        {
            var vm = LoadSampleSale();
            _saleApi.GetResponse = new SaleResponseDto { IsSuccess = false, Message = "Чек не знайдено." };
            vm.ReceiptNumberInput = "999";

            vm.FindSaleCommand.Execute(null);

            Assert.Empty(vm.Items);
        }

        [Fact]
        public void FindSaleCommand_ServerThrows_ShowsConnectionError()
        {
            var vm = CreateViewModel();
            _saleApi.Exception = new HttpRequestException("Connection refused");
            vm.ReceiptNumberInput = "15";

            vm.FindSaleCommand.Execute(null);

            Assert.StartsWith("Помилка зв'язку з сервером", _dialog.LastMessage);
        }

        [Fact]
        public void IncreaseReturnQuantityCommand_BelowAvailable_IncreasesQuantity()
        {
            var vm = LoadSampleSale(returnedFirst: 1);

            vm.IncreaseReturnQuantityCommand.Execute(vm.Items[0]);

            Assert.Equal(1, vm.Items[0].QuantityToReturn);
        }

        [Fact]
        public void IncreaseReturnQuantityCommand_AtAvailable_DoesNotExceedLimit()
        {
            var vm = LoadSampleSale(returnedFirst: 2);

            vm.IncreaseReturnQuantityCommand.Execute(vm.Items[0]);
            vm.IncreaseReturnQuantityCommand.Execute(vm.Items[0]);

            Assert.Equal(1, vm.Items[0].QuantityToReturn);
        }

        [Fact]
        public void IncreaseReturnQuantityCommand_FullyReturnedItem_StaysZero()
        {
            var vm = LoadSampleSale(returnedFirst: 3);

            vm.IncreaseReturnQuantityCommand.Execute(vm.Items[0]);

            Assert.Equal(0, vm.Items[0].QuantityToReturn);
        }

        [Fact]
        public void DecreaseReturnQuantityCommand_AboveZero_DecreasesQuantity()
        {
            var vm = LoadSampleSale();
            vm.IncreaseReturnQuantityCommand.Execute(vm.Items[0]);
            vm.IncreaseReturnQuantityCommand.Execute(vm.Items[0]);

            vm.DecreaseReturnQuantityCommand.Execute(vm.Items[0]);

            Assert.Equal(1, vm.Items[0].QuantityToReturn);
        }

        [Fact]
        public void DecreaseReturnQuantityCommand_AtZero_StaysZero()
        {
            var vm = LoadSampleSale();

            vm.DecreaseReturnQuantityCommand.Execute(vm.Items[0]);

            Assert.Equal(0, vm.Items[0].QuantityToReturn);
        }

        [Fact]
        public void ProcessReturnCommand_NoSaleLoaded_ShowsWarning()
        {
            var vm = CreateViewModel();

            vm.ProcessReturnCommand.Execute(null);

            Assert.Equal("Спочатку знайдіть чек за номером!", _dialog.LastMessage);
        }

        [Fact]
        public void ProcessReturnCommand_NothingSelected_ShowsWarning()
        {
            var vm = LoadSampleSale();

            vm.ProcessReturnCommand.Execute(null);

            Assert.Equal("Оберіть хоча б одну позицію для повернення!", _dialog.LastMessage);
        }

        [Fact]
        public void ProcessReturnCommand_NothingSelected_DoesNotCallReturnApi()
        {
            var vm = LoadSampleSale();

            vm.ProcessReturnCommand.Execute(null);

            Assert.Null(_saleApi.LastReturnRequest);
        }

        [Fact]
        public void ProcessReturnCommand_Success_SendsOnlySelectedItems()
        {
            var vm = LoadSampleSale();
            vm.IncreaseReturnQuantityCommand.Execute(vm.Items[0]);
            vm.IncreaseReturnQuantityCommand.Execute(vm.Items[0]);
            _saleApi.ReturnResponse = new SaleResponseDto
            {
                IsSuccess = true,
                Sale = SampleSale(15, refundedTotal: 960m, returnedFirst: 2)
            };

            vm.ProcessReturnCommand.Execute(null);

            Assert.Single(_saleApi.LastReturnRequest!.Items);
        }

        [Fact]
        public void ProcessReturnCommand_Success_SendsCorrectQuantity()
        {
            var vm = LoadSampleSale();
            vm.IncreaseReturnQuantityCommand.Execute(vm.Items[0]);
            vm.IncreaseReturnQuantityCommand.Execute(vm.Items[0]);
            _saleApi.ReturnResponse = new SaleResponseDto
            {
                IsSuccess = true,
                Sale = SampleSale(15, refundedTotal: 960m, returnedFirst: 2)
            };

            vm.ProcessReturnCommand.Execute(null);

            Assert.Equal(2, _saleApi.LastReturnRequest!.Items[0].Quantity);
        }

        [Fact]
        public void ProcessReturnCommand_Success_UpdatesRefundedTotal()
        {
            var vm = LoadSampleSale();
            vm.IncreaseReturnQuantityCommand.Execute(vm.Items[0]);
            _saleApi.ReturnResponse = new SaleResponseDto
            {
                IsSuccess = true,
                Sale = SampleSale(15, refundedTotal: 480m, returnedFirst: 1)
            };

            vm.ProcessReturnCommand.Execute(null);

            Assert.Equal(480m, vm.RefundedTotal);
        }

        [Fact]
        public void ProcessReturnCommand_Success_UpdatesAvailableQuantity()
        {
            var vm = LoadSampleSale();
            vm.IncreaseReturnQuantityCommand.Execute(vm.Items[0]);
            _saleApi.ReturnResponse = new SaleResponseDto
            {
                IsSuccess = true,
                Sale = SampleSale(15, refundedTotal: 480m, returnedFirst: 1)
            };

            vm.ProcessReturnCommand.Execute(null);

            Assert.Equal(2, vm.Items[0].AvailableToReturn);
        }

        [Fact]
        public void ProcessReturnCommand_Success_ShowsRefundedSumInMessage()
        {
            var vm = LoadSampleSale();
            vm.IncreaseReturnQuantityCommand.Execute(vm.Items[0]);
            _saleApi.ReturnResponse = new SaleResponseDto
            {
                IsSuccess = true,
                Sale = SampleSale(15, refundedTotal: 480m, returnedFirst: 1)
            };

            vm.ProcessReturnCommand.Execute(null);

            Assert.Contains("480", _dialog.LastMessage);
        }

        [Fact]
        public void ProcessReturnCommand_ApiFails_ShowsServerMessage()
        {
            var vm = LoadSampleSale();
            vm.IncreaseReturnQuantityCommand.Execute(vm.Items[0]);
            _saleApi.ReturnResponse = new SaleResponseDto
            {
                IsSuccess = false,
                Message = "Позицію вже повернено."
            };

            vm.ProcessReturnCommand.Execute(null);

            Assert.Equal("Позицію вже повернено.", _dialog.LastMessage);
        }

        [Fact]
        public void ProcessReturnCommand_ServerThrows_ShowsErrorMessage()
        {
            var vm = LoadSampleSale();
            vm.IncreaseReturnQuantityCommand.Execute(vm.Items[0]);
            _saleApi.Exception = new HttpRequestException("Timeout");

            vm.ProcessReturnCommand.Execute(null);

            Assert.StartsWith("Виникла помилка під час повернення", _dialog.LastMessage);
        }

        [Fact]
        public void ShowReturnsTabCommand_SwitchesMainViewModelToReturnsTab()
        {
            var mainVm = new MainViewModel(new AuthResponseDto(), new FakeProductApiClient(), _dialog, _saleApi);

            mainVm.ShowReturnsTabCommand.Execute(null);

            Assert.True(mainVm.IsReturnsTabActive);
        }

        [Fact]
        public void ShowPosTabCommand_SwitchesMainViewModelBackToPosTab()
        {
            var mainVm = new MainViewModel(new AuthResponseDto(), new FakeProductApiClient(), _dialog, _saleApi);
            mainVm.ShowReturnsTabCommand.Execute(null);

            mainVm.ShowPosTabCommand.Execute(null);

            Assert.True(mainVm.IsPosTabActive);
        }
    }
}