// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace IoT.DriverCore.S7PlcRx.Tests.Testing;

/// <summary>Creates value constraints.</summary>
public static class Is
{
    /// <summary>Gets a constraint for <see langword="true"/>.</summary>
    public static IConstraint True => new Constraint(static actual => actual is bool value && value, "true");

    /// <summary>Gets a constraint for <see langword="false"/>.</summary>
    public static IConstraint False => new Constraint(static actual => actual is bool value && !value, "false");

    /// <summary>Gets a constraint for a null value.</summary>
    public static IConstraint NullValue => new Constraint(static actual => actual is null, "null");

    /// <summary>Gets a constraint for an empty value.</summary>
    public static IConstraint EmptyValue => new Constraint(AssertionHelpers.IsEmpty, "empty");

    /// <summary>Gets a constraint for an equal value.</summary>
    /// <returns>The result.</returns>
    /// <param name="expected">Describes parameter expected for helper member 60.</param>
    public static EqualConstraint EqualTo(object? expected) => new(expected);

    /// <summary>Gets a constraint for a greater value.</summary>
    /// <param name="expected">Describes parameter expected for helper member 61.</param>
    /// <returns>The result.</returns>
    public static IConstraint GreaterThan(object expected) =>
        CreateComparison(expected, static value => value > 0, "greater than");

    /// <summary>Gets a constraint for a greater or equal value.</summary>
    /// <param name="expected">Describes parameter expected for helper member 62.</param>
    /// <returns>The result.</returns>
    public static IConstraint GreaterThanOrEqualTo(object expected) =>
        CreateComparison(expected, static value => value >= 0, "greater than or equal to");

    /// <summary>Gets a constraint for a lesser value.</summary>
    /// <param name="expected">Describes parameter expected for helper member 63.</param>
    /// <returns>The result.</returns>
    public static IConstraint LessThan(object expected) =>
        CreateComparison(expected, static value => value < 0, "less than");

    /// <summary>Gets a constraint for a lesser or equal value.</summary>
    /// <param name="expected">Describes parameter expected for helper member 64.</param>
    /// <returns>The result.</returns>
    public static IConstraint LessThanOrEqualTo(object expected) =>
        CreateComparison(expected, static value => value <= 0, "less than or equal to");

    /// <summary>Gets a constraint for a value in the supplied inclusive range.</summary>
    /// <param name="minimum">Describes parameter minimum for helper member 65.</param>
    /// <param name="maximum">Describes parameter maximum for helper member 66.</param>
    /// <returns>The result.</returns>
    public static IConstraint InRange(object minimum, object maximum) =>
        new Constraint(
            actual => AssertionHelpers.Compare(actual, minimum) >= 0 &&
                AssertionHelpers.Compare(actual, maximum) <= 0,
            $"in range {AssertionHelpers.Format(minimum)}..{AssertionHelpers.Format(maximum)}");

    /// <summary>Gets a constraint for an instance of the supplied type.</summary>
    /// <typeparam name="T">The type parameter.</typeparam>
    /// <param name="typeMarker">Describes parameter typeMarker for helper member 67.</param>
    /// <returns>The result.</returns>
    public static IConstraint InstanceOf<T>(params T[] typeMarker)
    {
        _ = typeMarker;
        return new Constraint(actual => actual is T, $"instance of {typeof(T).FullName}");
    }

    /// <summary>Gets a constraint for a value assignable to the supplied type.</summary>
    /// <typeparam name="T">The type parameter.</typeparam>
    /// <param name="typeMarker">Describes parameter typeMarker for helper member 68.</param>
    /// <returns>The result.</returns>
    public static IConstraint AssignableTo<T>(params T[] typeMarker)
    {
        _ = typeMarker;
        return new Constraint(actual => actual is T, $"assignable to {typeof(T).FullName}");
    }

    /// <summary>Gets a constraint for a value whose runtime type matches the supplied type.</summary>
    /// <typeparam name="T">The type parameter.</typeparam>
    /// <param name="typeMarker">Describes parameter typeMarker for helper member 69.</param>
    /// <returns>The result.</returns>
    public static IConstraint TypeOf<T>(params T[] typeMarker)
    {
        _ = typeMarker;
        return new Constraint(actual => actual?.GetType() == typeof(T), $"type {typeof(T).FullName}");
    }

    /// <summary>Gets a constraint for equivalent sequences.</summary>
    /// <param name="expected">Describes parameter expected for helper member 70.</param>
    /// <returns>The result.</returns>
    public static IConstraint EquivalentTo(object? expected) =>
        new Constraint(actual => AssertionHelpers.AreEquivalent(actual, expected), "an equivalent value");

    /// <summary>Gets a constraint for an equivalent typed sequence.</summary>
    /// <typeparam name="T">The type parameter.</typeparam>
    /// <returns>The result.</returns>
    /// <param name="expected">Describes parameter expected for helper member 71.</param>
    public static IConstraint EquivalentTo<T>(IEnumerable<T> expected) => EquivalentTo((object?)expected);

    /// <summary>Gets a constraint for the same object reference.</summary>
    /// <param name="expected">Describes parameter expected for helper member 72.</param>
    /// <returns>The result.</returns>
    public static IConstraint SameAs(object? expected) =>
        new Constraint(
            actual => ReferenceEquals(actual, expected),
            $"same reference as {AssertionHelpers.Format(expected)}");

    /// <summary>Creates a comparison constraint.</summary>
    /// <param name="expected">Describes parameter expected for helper member 73.</param>
    /// <param name="predicate">Describes parameter predicate for helper member 74.</param>
    /// <param name="expectation">Describes parameter expectation for helper member 75.</param>
    /// <returns>The result.</returns>
    private static Constraint CreateComparison(
        object expected,
        Func<int, bool> predicate,
        string expectation) =>
        new Constraint(
            actual => predicate(AssertionHelpers.Compare(actual, expected)),
            $"{expectation} {AssertionHelpers.Format(expected)}");

    /// <summary>Creates negated value constraints.</summary>
    public static class Not
    {
        /// <summary>Gets a constraint for a non-null value.</summary>
        public static readonly IConstraint Null = new Constraint(static actual => actual is not null, "not null");

        /// <summary>Gets a constraint for a non-empty value.</summary>
        public static readonly IConstraint Empty =
            new Constraint(static actual => !AssertionHelpers.IsEmpty(actual), "not empty");
    }
}
