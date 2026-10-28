using LogWindowUI.ViewModels;

namespace LogWindowUI.Tests
{
    public class RelayCommandTests
    {
        [Fact]
        public void Constructor_NullExecute_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RelayCommand(null!));
        }

        [Fact]
        public void CanExecute_WithoutPredicate_ReturnsTrue()
        {
            Assert.True(new RelayCommand(_ => { }).CanExecute(null));
        }

        [Fact]
        public void Execute_PassesParameterToAction()
        {
            object? received = null;
            var command = new RelayCommand(p => received = p);

            command.Execute("parameter");

            Assert.Equal("parameter", received);
        }

        [Fact]
        public void CanExecuteChanged_SubscribeAndUnsubscribe_DoesNotThrow()
        {
            var command = new RelayCommand(_ => { });
            EventHandler handler = (_, _) => { };

            var exception = Record.Exception(() =>
            {
                command.CanExecuteChanged += handler;
                command.CanExecuteChanged -= handler;
            });

            Assert.Null(exception);
        }
    }
}
