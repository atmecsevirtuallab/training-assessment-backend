SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.SessionProgrammingExercises', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SessionProgrammingExercises
    (
        SessionProgrammingExerciseId INT IDENTITY(1,1) PRIMARY KEY,
        SessionId NVARCHAR(30) NOT NULL,
        ItemLabel NVARCHAR(100) NOT NULL,
        Question NVARCHAR(MAX) NOT NULL,
        StarterCode NVARCHAR(MAX) NOT NULL CONSTRAINT DF_SessionProgrammingExercises_StarterCode DEFAULT '',
        TestCasesJson NVARCHAR(MAX) NOT NULL,
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_SessionProgrammingExercises_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_SessionProgrammingExercises UNIQUE(SessionId, ItemLabel)
    );
END;

IF COL_LENGTH('dbo.SessionProgrammingExercises', 'StarterCode') IS NULL
    ALTER TABLE dbo.SessionProgrammingExercises ADD StarterCode NVARCHAR(MAX) NOT NULL CONSTRAINT DF_SessionProgrammingExercises_StarterCode DEFAULT '';
GO

DECLARE @Exercises TABLE
(
    ItemLabel NVARCHAR(100),
    Question NVARCHAR(MAX),
    StarterCode NVARCHAR(MAX),
    TestCasesJson NVARCHAR(MAX)
);

INSERT INTO @Exercises VALUES
(N'Programming Exercise-1',
 N'<h2>Exercise 1: Product of Three Integers with Condition</h2><h3>Question</h3><p>Implement a program to calculate the product of three positive integer values. However, if one of the integers is 7, consider only the values to the right of 7 for calculation. If 7 is the last integer, display -1. <em>(Only one of the three values can be 7.)</em></p><h3>Hints</h3><p>Use conditional statements (if-else) to check the position of the number 7 in the array of inputs.</p><h3>Approach</h3><p>If the third element is 7, return -1. If the second is 7, return the third element. If the first is 7, return the product of the second and third. Otherwise, return the product of all three.</p>',
 N'import java.util.Scanner;

class Tester {
    public static void main(String[] args) {
        Scanner sc = new Scanner(System.in);
        int a = sc.nextInt();
        int b = sc.nextInt();
        int c = sc.nextInt();

        int[] nums = {a, b, c};
        int result;

        if (nums[2] == 7) {
            result = -1;
        } else if (nums[1] == 7) {
            // TODO: Complete the logic for when the 2nd number is 7
            result = 0;
        } else if (nums[0] == 7) {
            // TODO: Complete the logic for when the 1st number is 7
            result = 0;
        } else {
            // TODO: Complete the logic for when none of the numbers is 7
            result = 0;
        }

        System.out.println(result);
    }
}',
 N'[{"testCaseId":"TC-1","input":"1 5 3","output":"15"},{"testCaseId":"TC-2","input":"3 7 8","output":"8"},{"testCaseId":"TC-3","input":"7 2 9","output":"18"},{"testCaseId":"TC-4","input":"2 6 7","output":"-1"},{"testCaseId":"TC-5","input":"7 5 2","output":"10"}]'),
(N'Programming Exercise-2',
 N'<h2>Exercise 2: Star Pattern Printing</h2><h3>Question</h3><p>Implement a program to display a right-angled triangle pattern of stars in decreasing order.</p><h3>Hints</h3><p>Use nested loops. The outer loop controls the number of rows, and the inner loop prints the asterisks.</p><h3>Approach</h3><p>Run an outer loop from the number of rows down to 1. For each iteration, run an inner loop to print <code>*</code> exactly that many times, then print a newline.</p>',
 N'import java.util.Scanner;

class Tester {
    public static void main(String[] args) {
        Scanner sc = new Scanner(System.in);
        int rows = sc.nextInt();

        for (int i = rows; i >= 1; i--) {
            // TODO: Write the inner loop to print ''*'' exactly ''i'' times

            // TODO: Print a newline character to move to the next row
        }
    }
}',
 N'[{"testCaseId":"TC-1","input":"5","output":"*****\n****\n***\n**\n*"},{"testCaseId":"TC-2","input":"3","output":"***\n**\n*"},{"testCaseId":"TC-3","input":"1","output":"*"},{"testCaseId":"TC-4","input":"4","output":"****\n***\n**\n*"},{"testCaseId":"TC-5","input":"2","output":"**\n*"}]'),
(N'Programming Exercise-3',
 N'<h2>Exercise 3: Remove Duplicates and Spaces</h2><h3>Question</h3><p>Complete the <code>removeDuplicatesandSpaces()</code> method. Remove all duplicate characters and white spaces from the string passed to the method and return the modified string.</p><h3>Hints</h3><p>Use a HashSet to keep track of characters that have already been added.</p><h3>Approach</h3><p>Iterate through each character. Skip spaces. If the character is not in the HashSet, add it and append it to a StringBuilder.</p>',
 N'import java.util.HashSet;
import java.util.Scanner;
import java.util.Set;

class Tester {
    public static String removeDuplicatesandSpaces(String str) {
        StringBuilder sb = new StringBuilder();
        Set<Character> seen = new HashSet<>();

        // TODO: Iterate through each character of the string
        // TODO: Skip if the character is a whitespace character
        // TODO: If not seen, add to ''seen'' and append to ''sb''

        return sb.toString();
    }

    public static void main(String[] args) {
        Scanner sc = new Scanner(System.in);
        String str = sc.hasNextLine() ? sc.nextLine() : "";
        System.out.println(removeDuplicatesandSpaces(str));
    }
}',
 N'[{"testCaseId":"TC-1","input":"object oriented programming","output":"objectrindpgam"},{"testCaseId":"TC-2","input":"hello world","output":"helowrd"},{"testCaseId":"TC-3","input":"java programming","output":"javprogmin"},{"testCaseId":"TC-4","input":"a a a","output":"a"},{"testCaseId":"TC-5","input":"   ","output":""}]'),
(N'Programming Exercise-4',
 N'<h2>Exercise 4: Calculate Average of Three Numbers</h2><h3>Question</h3><p>Implement a class <code>Calculator</code> with the method <code>findAverage(int, int, int)</code> that calculates the average of three numbers and returns it rounded to two decimal digits.</p><h3>Hints</h3><p>Use <code>Math.round(number * 100.0) / 100.0</code> to round to two decimal places.</p><h3>Approach</h3><p>Sum the three integers and divide by 3.0. Multiply by 100, round using Math.round(), and divide by 100.0.</p>',
 N'import java.util.Scanner;

class Calculator {
    public double findAverage(int number1, int number2, int number3) {
        // TODO: Calculate the average of the three numbers
        // TODO: Round the average to two decimal digits and return it
        return 0.0;
    }
}

class Tester {
    public static void main(String[] args) {
        Scanner sc = new Scanner(System.in);
        int number1 = sc.nextInt();
        int number2 = sc.nextInt();
        int number3 = sc.nextInt();
        Calculator calc = new Calculator();
        System.out.println(calc.findAverage(number1, number2, number3));
    }
}',
 N'[{"testCaseId":"TC-1","input":"12 8 15","output":"11.67"},{"testCaseId":"TC-2","input":"10 20 30","output":"20.0"},{"testCaseId":"TC-3","input":"1 1 1","output":"1.0"},{"testCaseId":"TC-4","input":"0 0 0","output":"0.0"},{"testCaseId":"TC-5","input":"10 10 11","output":"10.33"}]'),
(N'Programming Exercise-5',
 N'<h2>Exercise 5: Movie Ticket Encapsulation</h2><h3>Question</h3><p>Implement the class <code>MovieTicket</code>. Apply a 2% tax to the total amount based on <code>movieId</code> and <code>noOfSeats</code>. Round the final amount. Return -1 for invalid IDs.</p><h3>Hints</h3><p>Map movieId to costPerTicket inside the calculation method.</p><h3>Approach</h3><p>Use if-else to set costPerTicket (111 to 7, 112 to 8, and 113 to 8.5). Calculate (cost × seats) plus 2% tax and return Math.round(amount).</p>',
 N'import java.util.Scanner;

class MovieTicket {
    private int movieId;
    private int noOfSeats;
    private double costPerTicket;

    public MovieTicket(int movieId, int noOfSeats) {
        this.movieId = movieId;
        this.noOfSeats = noOfSeats;
    }

    public double calculateTotalAmount() {
        // TODO: Set costPerTicket based on movieId (111, 112, 113).
        // If invalid, return -1.

        // TODO: Calculate total amount with 2% tax and return rounded value
        return -1;
    }
}

class Tester {
    public static void main(String[] args) {
        Scanner sc = new Scanner(System.in);
        MovieTicket movieTicket = new MovieTicket(sc.nextInt(), sc.nextInt());
        double amount = movieTicket.calculateTotalAmount();
        if (amount == -1)
            System.out.println("Sorry! Please enter valid movie Id and number of seats");
        else
            System.out.println("Total amount for booking : $" + amount);
    }
}',
 N'[{"testCaseId":"TC-1","input":"112 3","output":"Total amount for booking : $24.0"},{"testCaseId":"TC-2","input":"114 3","output":"Sorry! Please enter valid movie Id and number of seats"},{"testCaseId":"TC-3","input":"111 2","output":"Total amount for booking : $14.0"},{"testCaseId":"TC-4","input":"113 4","output":"Total amount for booking : $35.0"},{"testCaseId":"TC-5","input":"111 0","output":"Total amount for booking : $0.0"}]');

MERGE dbo.SessionProgrammingExercises AS target
USING (SELECT N'DSA-S1' SessionId, ItemLabel, Question, StarterCode, TestCasesJson FROM @Exercises) AS source
ON target.SessionId = source.SessionId AND target.ItemLabel = source.ItemLabel
WHEN MATCHED THEN UPDATE SET
    Question = source.Question,
    StarterCode = source.StarterCode,
    TestCasesJson = source.TestCasesJson,
    UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (SessionId, ItemLabel, Question, StarterCode, TestCasesJson, UpdatedAt)
VALUES (source.SessionId, source.ItemLabel, source.Question, source.StarterCode, source.TestCasesJson, SYSUTCDATETIME());

SELECT SessionId, ItemLabel, LEN(Question) QuestionLength, LEN(StarterCode) StarterCodeLength,
       (SELECT COUNT(*) FROM OPENJSON(TestCasesJson)) TestCaseCount
FROM dbo.SessionProgrammingExercises
WHERE SessionId = N'DSA-S1'
ORDER BY ItemLabel;
