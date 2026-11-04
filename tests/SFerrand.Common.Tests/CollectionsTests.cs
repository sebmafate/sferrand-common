using SFerrand.Common.Collections;

namespace SFerrand.Common.Tests;

[TestClass]
public class CollectionsTests
{
    private sealed record Person(string Name, int Age);

    #region ListExtensions

    [TestMethod]
    public void AddRange_AppendsItems_OnAnIListWithoutAddRange()
    {
        IList<int> list = new System.Collections.ObjectModel.Collection<int> { 1 };
        list.AddRange(new[] { 2, 3 });
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, list.ToArray());
    }

    [TestMethod]
    public void AddRange_WithItselfAsSource_DoesNotThrow()
    {
        IList<int> list = new System.Collections.ObjectModel.Collection<int> { 1, 2 };
        list.AddRange(list);
        CollectionAssert.AreEqual(new[] { 1, 2, 1, 2 }, list.ToArray());
    }

    [TestMethod]
    public void RemoveAll_RemovesEveryMatch_AndReturnsTheCount()
    {
        IList<int> list = new System.Collections.ObjectModel.Collection<int> { 1, 2, 2, 3, 4, 4, 5 };
        var removed = list.RemoveAll(i => i % 2 == 0);

        Assert.AreEqual(4, removed);
        CollectionAssert.AreEqual(new[] { 1, 3, 5 }, list.ToArray());
    }

    [TestMethod]
    public void RemoveAll_WithNoMatch_LeavesTheListAlone()
    {
        IList<int> list = new System.Collections.ObjectModel.Collection<int> { 1, 3 };
        Assert.AreEqual(0, list.RemoveAll(i => i > 10));
        Assert.HasCount(2, list);
    }

    [TestMethod]
    public void RemoveRange_RemovesOneOccurrencePerItem()
    {
        IList<int> list = new System.Collections.ObjectModel.Collection<int> { 1, 2, 2, 3 };
        var removed = list.RemoveRange(new[] { 2, 3, 9 });

        Assert.AreEqual(2, removed);
        CollectionAssert.AreEqual(new[] { 1, 2 }, list.ToArray());
    }

    #endregion

    #region Comparers

    [TestMethod]
    public void GenericComparer_Distinct_UsesTheKey()
    {
        var people = new[] { new Person("Ada", 36), new Person("Ada", 40), new Person("Alan", 41) };

        var distinct = people.Distinct(new GenericComparer<Person>(p => p.Name)).ToArray();

        Assert.HasCount(2, distinct);
    }

    [TestMethod]
    public void GenericComparer_HashCode_IsConsistentWithEquals()
    {
        var comparer = new GenericComparer<Person>(p => p.Name);
        var a = new Person("Ada", 36);
        var b = new Person("Ada", 40);

        Assert.IsTrue(comparer.Equals(a, b));
        Assert.AreEqual(comparer.GetHashCode(a), comparer.GetHashCode(b));

        var set = new HashSet<Person>(comparer) { a };
        Assert.IsFalse(set.Add(b));
    }

    [TestMethod]
    public void GenericComparer_HandlesNullsAndNullKeys()
    {
        var comparer = new GenericComparer<Person>(p => p.Name == "none" ? null : p.Name);

        Assert.IsTrue(comparer.Equals(null, null));
        Assert.IsFalse(comparer.Equals(new Person("Ada", 1), null));
        Assert.IsTrue(comparer.Equals(new Person("none", 1), new Person("none", 2)));
        Assert.AreEqual(0, comparer.GetHashCode(new Person("none", 1)));
    }

    [TestMethod]
    public void Comparers_TreatNullsAndSameReferencesConsistently()
    {
        var person = new Person("Ada", 36);
        IEqualityComparer<Person> byName = new GenericComparer<Person>(p => p.Name);
        IEqualityComparer<Person> byPredicate = new FuncComparer<Person>((x, y) => x.Name == y.Name);

        foreach (var comparer in new[] { byName, byPredicate })
        {
            Assert.IsTrue(comparer.Equals(person, person));
            Assert.IsTrue(comparer.Equals(null, null));
            Assert.IsFalse(comparer.Equals(person, null));
            Assert.IsFalse(comparer.Equals(null, person));
            Assert.ThrowsExactly<ArgumentNullException>(() => comparer.GetHashCode(null!));
        }
    }

    [TestMethod]
    public void Comparers_RejectNullDelegates()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new GenericComparer<Person>(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new FuncComparer<Person>(null!));
    }

    [TestMethod]
    public void FuncComparer_WithoutHash_StaysCorrectInHashSets()
    {
        var comparer = new FuncComparer<Person>((x, y) => x.Age / 10 == y.Age / 10);
        var set = new HashSet<Person>(comparer) { new("Ada", 36), new("Alan", 31), new("Grace", 45) };

        Assert.HasCount(2, set);
    }

    [TestMethod]
    public void FuncComparer_WithHash_UsesIt()
    {
        var comparer = new FuncComparer<Person>((x, y) => x.Age == y.Age, p => p.Age);

        Assert.AreEqual(36, comparer.GetHashCode(new Person("Ada", 36)));
        Assert.IsTrue(comparer.Equals(new Person("A", 36), new Person("B", 36)));
    }

    #endregion
}
