using Moq;
using Subscription_Service.Models;
using Subscription_Service.Services;
using Subscription_Service.Services.Interfaces;
using Xunit;

namespace SubscriptionServiceTests
{
    public class SubscriptionServiceTests
    {
        private readonly Mock<IMemberRepository> _repo = new();
        private readonly Mock<IPaymentService> _payment = new();
        private readonly Mock<INotificationService> _notify = new();
        private readonly SubscriptionService _service;

        public SubscriptionServiceTests()
        {
            _service = new SubscriptionService(_repo.Object, _payment.Object, _notify.Object);
        }

        /// <summary>RenewSubscription кидає ArgumentException, якщо учасника не знайдено, і не перевіряє оплату</summary>
        [Fact]
        public void RenewSubscription_MemberNotFound_ThrowsArgumentException()
        {
            // Arrange
            _repo.Setup(r => r.GetById(It.IsAny<int>())).Returns((Member?)null);

            // Act
            var ex = Assert.Throws<ArgumentException>(() => _service.RenewSubscription(99, 100m, 30));

            // Assert
            Assert.Equal("Member not found", ex.Message);
            _payment.Verify(p => p.VerifyPayment(It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
            _repo.Verify(r => r.Update(It.IsAny<Member>()), Times.Never);
        }

        /// <summary>RenewSubscription повертає false, якщо оплата не пройшла, і не оновлює учасника та не надсилає сповіщення</summary>
        [Fact]
        public void RenewSubscription_PaymentFailed_ReturnsFalseAndDoesNotUpdate()
        {
            // Arrange
            var member = new Member { Id = 1, IsActive = false };
            _repo.Setup(r => r.GetById(1)).Returns(member);
            _payment.Setup(p => p.VerifyPayment(1, It.IsAny<decimal>())).Returns(false);

            // Act
            var result = _service.RenewSubscription(1, 50m, 30);

            // Assert
            Assert.False(result);
            Assert.False(member.IsActive);
            Assert.Null(member.SubscriptionEnd);
            _repo.Verify(r => r.Update(It.IsAny<Member>()), Times.Never);
            _notify.Verify(n => n.SendNotification(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        /// <summary>RenewSubscription при успішній оплаті повертає true, активує учасника та зберігає його в репозиторії</summary>
        [Fact]
        public void RenewSubscription_PaymentSucceeded_ReturnsTrueAndActivatesMember()
        {
            // Arrange
            var member = new Member { Id = 1, IsActive = false };
            _repo.Setup(r => r.GetById(1)).Returns(member);
            _payment.Setup(p => p.VerifyPayment(1, 100m)).Returns(true);

            // Act
            var result = _service.RenewSubscription(1, 100m, 30);

            // Assert
            Assert.True(result);
            Assert.True(member.IsActive);
            Assert.NotNull(member.SubscriptionEnd);
            _repo.Verify(r => r.Update(It.Is<Member>(m => m.Id == 1 && m.IsActive)), Times.Once);
        }

        /// <summary>RenewSubscription встановлює дату завершення підписки рівно на вказану кількість днів від поточного моменту</summary>
        [Theory]
        [InlineData(1)]
        [InlineData(30)]
        [InlineData(365)]
        public void RenewSubscription_SetsSubscriptionEndByDays(int days)
        {
            // Arrange
            var member = new Member { Id = 7 };
            _repo.Setup(r => r.GetById(7)).Returns(member);
            _payment.Setup(p => p.VerifyPayment(It.IsAny<int>(), It.IsAny<decimal>())).Returns(true);
            var before = DateTime.Now.AddDays(days);

            // Act
            _service.RenewSubscription(7, 10m, days);
            var after = DateTime.Now.AddDays(days);

            // Assert
            Assert.NotNull(member.SubscriptionEnd);
            Assert.True(member.SubscriptionEnd >= before && member.SubscriptionEnd <= after);
        }

        /// <summary>RenewSubscription надсилає рівно одне сповіщення "Subscription renewed!" саме цьому учаснику</summary>
        [Fact]
        public void RenewSubscription_Success_SendsRenewalNotification()
        {
            // Arrange
            _repo.Setup(r => r.GetById(3)).Returns(new Member { Id = 3 });
            _payment.Setup(p => p.VerifyPayment(3, It.IsAny<decimal>())).Returns(true);

            // Act
            _service.RenewSubscription(3, 99.99m, 14);

            // Assert
            _notify.Verify(n => n.SendNotification("Subscription renewed!", 3), Times.Once);
            _notify.Verify(n => n.SendNotification("Membership expired", It.IsAny<int>()), Times.Never);
        }

        /// <summary>RenewSubscription передає в сервіс оплати правильні Id учасника та суму</summary>
        [Fact]
        public void RenewSubscription_PassesCorrectArgumentsToPaymentService()
        {
            // Arrange
            _repo.Setup(r => r.GetById(10)).Returns(new Member { Id = 10 });
            _payment.Setup(p => p.VerifyPayment(It.IsAny<int>(), It.IsAny<decimal>())).Returns(true);

            // Act
            _service.RenewSubscription(10, 250m, 30);

            // Assert
            _payment.Verify(p => p.VerifyPayment(It.Is<int>(id => id == 10), It.Is<decimal>(a => a == 250m)), Times.Once);
            _payment.VerifyNoOtherCalls();
        }

        /// <summary>DeactivateExpiredMembers деактивує лише учасників із простроченою підпискою та надсилає їм сповіщення</summary>
        [Fact]
        public void DeactivateExpiredMembers_DeactivatesOnlyExpiredMembers()
        {
            // Arrange
            var expired1 = new Member { Id = 1, IsActive = true, SubscriptionEnd = DateTime.Now.AddDays(-5) };
            var expired2 = new Member { Id = 2, IsActive = true, SubscriptionEnd = DateTime.Now.AddDays(-1) };
            var valid = new Member { Id = 3, IsActive = true, SubscriptionEnd = DateTime.Now.AddDays(10) };
            var noDate = new Member { Id = 4, IsActive = true, SubscriptionEnd = null };
            _repo.Setup(r => r.GetAll()).Returns(new List<Member> { expired1, expired2, valid, noDate });

            // Act
            _service.DeactivateExpiredMembers();

            // Assert
            Assert.False(expired1.IsActive);
            Assert.False(expired2.IsActive);
            Assert.True(valid.IsActive);
            Assert.True(noDate.IsActive);
            _repo.Verify(r => r.Update(It.IsAny<Member>()), Times.Exactly(2));
            _notify.Verify(n => n.SendNotification("Membership expired", It.IsAny<int>()), Times.Exactly(2));
            _repo.Verify(r => r.Update(It.Is<Member>(m => m.Id == 3 || m.Id == 4)), Times.Never);
        }

        /// <summary>DeactivateExpiredMembers нічого не оновлює і не надсилає, якщо немає прострочених підписок</summary>
        [Fact]
        public void DeactivateExpiredMembers_NoExpiredMembers_DoesNothing()
        {
            // Arrange
            var members = new List<Member>
            {
                new Member { Id = 1, IsActive = true, SubscriptionEnd = DateTime.Now.AddDays(3) },
                new Member { Id = 2, IsActive = false, SubscriptionEnd = null }
            };
            _repo.Setup(r => r.GetAll()).Returns(members);

            // Act
            _service.DeactivateExpiredMembers();

            // Assert
            _repo.Verify(r => r.GetAll(), Times.Once);
            _repo.Verify(r => r.Update(It.IsAny<Member>()), Times.Never);
            _notify.Verify(n => n.SendNotification(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }
    }
}