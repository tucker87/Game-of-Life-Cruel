using Xunit;

namespace Game_of_Life_Cruel.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void CanGame()
        {
            //Arrange
            var game = new GameOfLife();
            //Act
            var result = game.handleSubmit();



            result.MoveNext();

            //Assert
        }
    }
}
