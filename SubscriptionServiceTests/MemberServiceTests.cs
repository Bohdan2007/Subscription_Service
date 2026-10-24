using Moq;
using Subscription_Service.Models;
using Subscription_Service.Services;
using Subscription_Service.Services.Interfaces;
using Xunit;

namespace SubscriptionServiceTests
{
    public class MemberServiceTests
    {
        private readonly Mock<IMemberRepository> _repo = new();
        private readonly MemberService _service;

        public MemberServiceTests()
        {
            _service = new MemberService(_repo.Object);
        }

        /// <summary>GetMember повертає учасника, якщо він існує в репозиторії</summary>
        [Fact]
        public void GetMember_ExistingId_ReturnsMember()
        {
            // Arrange
            var expected = new Member { Id = 1, Name = "Іван", IsActive = true };
            _repo.Setup(r => r.GetById(1)).Returns(expected);

            // Act
            var result = _service.GetMember(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expected.Id, result!.Id);
            Assert.Equal("Іван", result.Name);
            _repo.Verify(r => r.GetById(1), Times.Once);
        }

        /// <summary>GetMember повертає null, якщо учасника з таким Id не існує</summary>
        [Fact]
        public void GetMember_NonExistingId_ReturnsNull()
        {
            // Arrange
            _repo.Setup(r => r.GetById(It.IsAny<int>())).Returns((Member?)null);

            // Act
            var result = _service.GetMember(42);

            // Assert
            Assert.Null(result);
        }

        /// <summary> IsActive повертає true для активного учасника</summary>
        [Fact]
        public void IsActive_ActiveMember_ReturnsTrue()
        {
            // Arrange
            _repo.Setup(r => r.GetById(1)).Returns(new Member { Id = 1, IsActive = true });

            // Act
            var result = _service.IsActive(1);

            // Assert
            Assert.True(result);
        }

        /// <summary>IsActive повертає false для неактивного учасника</summary>
        [Fact]
        public void IsActive_InactiveMember_ReturnsFalse()
        {
            // Arrange
            _repo.Setup(r => r.GetById(2)).Returns(new Member { Id = 2, IsActive = false });

            // Act
            var result = _service.IsActive(2);

            // Assert
            Assert.False(result);
        }

        /// <summary>IsActive повертає false, якщо учасника не знайдено, і звертається до репозиторію рівно один раз</summary>
        [Fact]
        public void IsActive_MemberNotFound_ReturnsFalse()
        {
            // Arrange
            _repo.Setup(r => r.GetById(It.IsAny<int>())).Returns((Member?)null);

            // Act
            var result = _service.IsActive(100);

            // Assert
            Assert.False(result);
            _repo.Verify(r => r.GetById(It.IsAny<int>()), Times.Once);
        }

        /// <summary>IsActive повертає значення властивості IsActive учасника для різних вхідних даних</summary>
        [Theory]
        [InlineData(1, true)]
        [InlineData(2, false)]
        [InlineData(3, true)]
        public void IsActive_ReturnsMemberIsActiveValue(int id, bool isActive)
        {
            // Arrange
            _repo.Setup(r => r.GetById(id)).Returns(new Member { Id = id, IsActive = isActive });

            // Act
            var result = _service.IsActive(id);

            // Assert
            Assert.Equal(isActive, result);
            _repo.Verify(r => r.GetById(It.Is<int>(x => x == id)), Times.Once);
        }

        /// <summary>Кожен виклик GetMember звертається до репозиторію</summary>
        [Fact]
        public void GetMember_CalledTwice_RepositoryCalledExactlyTwice()
        {
            // Arrange
            _repo.Setup(r => r.GetById(5)).Returns(new Member { Id = 5 });

            // Act
            _service.GetMember(5);
            _service.GetMember(5);

            // Assert
            _repo.Verify(r => r.GetById(5), Times.Exactly(2));
            _repo.Verify(r => r.Update(It.IsAny<Member>()), Times.Never);
        }
    }
}