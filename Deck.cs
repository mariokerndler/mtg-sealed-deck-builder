namespace SealedDeckBuilder
{
    internal class Deck
    {
        public List<DeckEntry> MainDeck { get; } = [];
        public float DeckRating { get; private set; } = 0.0f;
    }

    internal record DeckEntry(int Amount, Card Card);
}
