using Library_Project.Model;
using Library_Project.Services;
using Library_Project.Services.Interfaces;
using Moq;

namespace Library_Project_Tests
{
    public class LibraryServiceTests
    {
        private readonly Mock<IBookRepository> _repoMock;
        private readonly Mock<IMemberService> _memberMock;
        private readonly Mock<INotificationService> _notifMock;
        private readonly LibraryService _service;

        public LibraryServiceTests()
        {
            _repoMock = new Mock<IBookRepository>();
            _memberMock = new Mock<IMemberService>();
            _notifMock = new Mock<INotificationService>();

            _service = new LibraryService(_repoMock.Object, _memberMock.Object, _notifMock.Object);
        }

        /// <summary>
        /// Перевіряє, що додавання книги з порожньою назвою викликає виняток ArgumentException.
        /// </summary>
        [Fact]
        public void AddBook_EmptyTitle_ThrowsArgumentException()
        {
            // Arrange
            string title = "";
            int copies = 5;

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                _service.AddBook(title, copies));
        }

        /// <summary>
        /// Перевіряє, що додавання книги з нульовою або від'ємною кількістю примірників викликає виняток ArgumentException.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-10)]
        public void AddBook_InvalidCopies_ThrowsArgumentException(int copies)
        {
            // Arrange
            string title = "Кобзар";

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                _service.AddBook(title, copies));
        }

        /// <summary>
        /// Перевіряє, що нова книга зберігається у репозиторії з правильною назвою та кількістю примірників.
        /// </summary>
        [Fact]
        public void AddBook_NewBook_SavesBook()
        {
            // Arrange
            string title = "Кобзар";
            int copies = 5;

            _repoMock.Setup(r => r.FindBook(title))
                     .Returns((Book)null!);

            // Act
            _service.AddBook(title, copies);

            // Assert
            _repoMock.Verify(r => r.SaveBook(
                It.Is<Book>(b =>
                    b.Title == title &&
                    b.Copies == copies)),
                Times.Once);
        }

        /// <summary>
        /// Перевіряє, що при додаванні примірників наявної книги їх кількість правильно збільшується.
        /// </summary>
        [Fact]
        public void AddBook_ExistingBook_IncreasesCopies()
        {
            // Arrange
            string title = "Кобзар";
            int additionalCopies = 5;

            var existingBook = new Book
            {
                Title = title,
                Copies = 3
            };

            _repoMock.Setup(r => r.FindBook(title))
                     .Returns(existingBook);

            // Act
            _service.AddBook(title, additionalCopies);

            // Assert
            Assert.Equal(8, existingBook.Copies);

            _repoMock.Verify(r => r.SaveBook(existingBook), Times.Once);
        }

        /// <summary>
        /// Перевіряє успішну видачу книги зареєстрованому
        /// читачеві та зменшення кількості примірників.
        /// </summary>
        [Fact]
        public void BorrowBook_ValidMemberAndAvailableBook_ReturnsTrue()
        {
            // Arrange
            int memberId = 1;
            string title = "Кобзар";

            var book = new Book
            {
                Title = title,
                Copies = 5
            };

            _memberMock.Setup(m => m.IsValidMember(memberId))
                       .Returns(true);

            _repoMock.Setup(r => r.FindBook(title))
                     .Returns(book);

            // Act
            bool result = _service.BorrowBook(memberId, title);

            // Assert
            Assert.True(result);
            Assert.Equal(4, book.Copies);

            _repoMock.Verify(r => r.SaveBook(book), Times.Once);

            _notifMock.Verify(n => n.NotifyBorrow(memberId, title),
                              Times.Once);
        }

        /// <summary>
        /// Перевіряє, що при спробі взяти відсутню книгу
        /// метод повертає false, не зберігає змін
        /// і не надсилає сповіщення.
        /// </summary>
        [Fact]
        public void BorrowBook_BookNotFound_ReturnsFalse()
        {
            // Arrange
            int memberId = 1;
            string title = "Кобзар";

            _memberMock.Setup(m => m.IsValidMember(memberId))
                       .Returns(true);

            _repoMock.Setup(r => r.FindBook(title))
                     .Returns((Book)null!);

            // Act
            bool result = _service.BorrowBook(memberId, title);

            // Assert
            Assert.False(result);

            _repoMock.Verify(
                r => r.SaveBook(It.IsAny<Book>()),
                Times.Never);

            _notifMock.Verify(
                n => n.NotifyBorrow(It.IsAny<int>(), It.IsAny<string>()),
                Times.Never);
        }

        /// <summary>
        /// Перевіряє, що незареєстрований читач не може взяти книгу, а репозиторій і сервіс сповіщень не виконують жодних операцій.
        /// </summary>
        [Fact]
        public void BorrowBook_InvalidMember_ThrowsInvalidOperationException()
        {
            // Arrange
            int memberId = 99;
            string title = "Кобзар";

            _memberMock.Setup(m => m.IsValidMember(memberId))
                       .Returns(false);

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                _service.BorrowBook(memberId, title));

            Assert.Equal("Invalid member.", exception.Message);

            _repoMock.Verify(
                r => r.FindBook(It.IsAny<string>()),
                Times.Never);

            _repoMock.Verify(
                r => r.SaveBook(It.IsAny<Book>()),
                Times.Never);

            _notifMock.Verify(
                n => n.NotifyBorrow(It.IsAny<int>(), It.IsAny<string>()),
                Times.Never);
        }

        /// <summary>
        /// Перевіряє, що книга з нульовою кількістю примірників не видається читачеві та не викликає збереження і сповіщення.
        /// </summary>
        [Fact]
        public void BorrowBook_NoAvailableCopies_ReturnsFalse()
        {
            // Arrange
            int memberId = 1;
            string title = "Кобзар";
            var book = new Book { Title = title, Copies = 0 };

            _memberMock.Setup(m => m.IsValidMember(memberId)).Returns(true);
            _repoMock.Setup(r => r.FindBook(title)).Returns(book);

            // Act
            bool result = _service.BorrowBook(memberId, title);

            // Assert
            Assert.False(result);
            Assert.Equal(0, book.Copies);

            _repoMock.Verify(r => r.SaveBook(It.IsAny<Book>()), Times.Never);
            _notifMock.Verify(n => n.NotifyBorrow(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// Перевіряє успішне повернення книги, збільшення кількості примірників, збереження змін та надсилання сповіщення.
        /// </summary>
        [Fact]
        public void ReturnBook_ExistingBook_ReturnsTrue()
        {
            // Arrange
            int memberId = 1;
            string title = "Кобзар";
            var book = new Book { Title = title, Copies = 3 };

            _repoMock.Setup(r => r.FindBook(title)).Returns(book);

            // Act
            bool result = _service.ReturnBook(memberId, title);

            // Assert
            Assert.True(result);
            Assert.Equal(4, book.Copies);
            _repoMock.Verify(r => r.SaveBook(book), Times.Once);
            _notifMock.Verify(n => n.NotifyReturn(memberId, title), Times.Once);
        }

        /// <summary>
        /// Перевіряє, що при поверненні відсутньої книги метод повертає false, не зберігає зміни та не надсилає сповіщення.
        /// </summary>
        [Fact]
        public void ReturnBook_BookNotFound_ReturnsFalse()
        {
            // Arrange
            int memberId = 1;
            string title = "Кобзар";

            _repoMock.Setup(r => r.FindBook(title)).Returns((Book)null!);

            // Act
            bool result = _service.ReturnBook(memberId, title);

            // Assert
            Assert.False(result);
            _repoMock.Verify(r => r.FindBook(title), Times.Once);
            _repoMock.Verify(r => r.SaveBook(It.IsAny<Book>()), Times.Never);
            _notifMock.Verify(n => n.NotifyReturn(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// Перевіряє, що метод повертає лише доступні книги, у яких кількість примірників більша за нуль.
        /// </summary>
        [Fact]
        public void GetAvailableBooks_AvailableBooksExist_ReturnsOnlyAvailableBooks()
        {
            // Arrange
            var books = new List<Book>
            {
                new Book { Title = "Кобзар", Copies = 5 },
                new Book { Title = "Гаррі Поттер", Copies = 0 },
                new Book { Title = "Лісова пісня", Copies = 3 }
            };

            _repoMock.Setup(r => r.GetAllBooks()).Returns(books);

            // Act
            var result = _service.GetAvailableBooks();

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.Equal(2, result.Count);
            Assert.Contains(result, b => b.Title == "Кобзар");
            Assert.Contains(result, b => b.Title == "Лісова пісня");
            Assert.DoesNotContain(result, b => b.Title == "Гаррі Поттер");

            _repoMock.Verify(r => r.GetAllBooks(), Times.Once);
        }

        /// <summary>
        /// Перевіряє, що метод повертає порожній список, якщо всі книги мають нульову кількість примірників.
        /// </summary>
        [Fact]
        public void GetAvailableBooks_NoAvailableBooks_ReturnsEmptyList()
        {
            // Arrange
            var books = new List<Book>
            {
                new Book { Title = "Кобзар", Copies = 0 },
                new Book { Title = "Гаррі Поттер", Copies = 0 },
                new Book { Title = "Лісова пісня", Copies = 0 }
            };

            _repoMock.Setup(r => r.GetAllBooks()).Returns(books);

            // Act
            var result = _service.GetAvailableBooks();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            Assert.Equal(0, result.Count);

            _repoMock.Verify(r => r.GetAllBooks(), Times.Once);
        }

        /// <summary>
        /// Перевіряє, що додавання примірників змінює їх кількість, а збереження книги виконується рівно один раз.
        /// </summary>
        [Fact]
        public void AddBook_ExistingBook_UpdatesCopiesAndSavesExactlyOnce()
        {
            // Arrange
            string title = "Кобзар";
            var book = new Book { Title = title, Copies = 2 };

            _repoMock.Setup(r => r.FindBook(title)).Returns(book);

            // Act
            _service.AddBook(title, 3);

            // Assert
            Assert.Equal(5, book.Copies);
            Assert.NotEqual(2, book.Copies);
            _repoMock.Verify(r => r.SaveBook(book), Times.Exactly(1));
        }
    }
}
