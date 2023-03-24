using LabSqlParser.AstNode;
using System;
using System.Collections.Generic;
using System.Linq;
namespace LabSqlParser;
sealed class Parser {
	readonly IReadOnlyList<Token> tokens;
	int pos;
	public Parser(IReadOnlyList<Token> tokens) {
		this.tokens = tokens;
	}
	#region common
	Exception MakeError(string message) {
		throw new InvalidOperationException($"{message} at {pos}");
	}
	void ReadNextToken() {
		pos++;
	}
	Token CurrentToken => tokens[pos];
	void Expect(string s) {
		if (CurrentToken.Lexeme != s) {
			throw MakeError($"Ожидалось {s} Получено {CurrentToken.Lexeme}");
		}
		ReadNextToken();
	}
	bool SkipIf(string s) {
		if (CurrentToken.Lexeme == s) {
			ReadNextToken();
			return true;
		}
		return false;
	}
	void ExpectEof() {
		if (CurrentToken.Type != TokenType.EndOfFile) {
			throw MakeError($"Ожидался конец файла, получен {CurrentToken}");
		}
	}
	#endregion
	public static List<Token> PrepareTokens(IEnumerable<Token> tokens) {
		return tokens.Where(token => token.Type != TokenType.Spaces).Append(new Token(TokenType.EndOfFile, "\x00")).ToList();
	}
	public static Insert Parse(IEnumerable<Token> tokens) {
		var tokenList = PrepareTokens(tokens);
		var parser = new Parser(tokenList);
		var insert = parser.ParseInsert();
		parser.ExpectEof();
		return insert;
	}
	Insert ParseInsert() {
		Expect("INSERT");
		Expect("INTO");
		var tableName = ParseIdentifier();
		Expect("VALUES");
		Expect("(");
		var row = ParseExpressions();
		Expect(")");
		return new Insert(tableName, row);
	}
	List<IExpression> ParseExpressions() {
		var expressions = new List<IExpression> { ParseExpression() };
		while (SkipIf(",")) {
			expressions.Add(ParseExpression());
		}
		return expressions;
	}
	IExpression ParseExpression() {
		var left = ParseAdditive();
		while (SkipIf("=")) {
			var right = ParseAdditive();
			left = new BinaryOperation(left, BinaryOperationType.Equal, right);
		}
		return left;
	}
	IExpression ParseAdditive() {
		var left = ParseMultiplicative();
		while (SkipIf("-")) {
			var right = ParseMultiplicative();
			left = new BinaryOperation(left, BinaryOperationType.Minus, right);
		}
		return left;
	}
	IExpression ParseMultiplicative() {
		var left = ParsePrimary();
		while (CurrentToken.Lexeme == "/" || CurrentToken.Lexeme == "%") {
			var binaryOperator = ParseBinaryOperator();
			var right = ParsePrimary();
			left = new BinaryOperation(left, binaryOperator, right);
		}
		return left;
	}
	IExpression ParsePrimary() {
		if (SkipIf("(")) {
			var primary = CurrentToken.Lexeme == "SELECT" ? ParseSelect() : ParseExpression();
			Expect(")");
			return primary;
		}
		if (CurrentToken.Type == TokenType.Identifier) {
			return ParseIdentifier();
		}
		if (CurrentToken.Type == TokenType.Number) {
			return ParseNumber();
		}
		throw MakeError("Ожидалось число или идентификатор или выражение в скобках.");
	}
	IExpression ParseSelect() {
		Expect("SELECT");
		var column = ParseExpression();
		IExpression? where = null;
		if (SkipIf("WHERE")) {
			where = ParseExpression();
		}
		return new Select(column, where);
	}
	BinaryOperationType ParseBinaryOperator() {
		var binOp = CurrentToken.Lexeme switch {
			"/" => BinaryOperationType.Division,
			"%" => BinaryOperationType.Module,
			"-" => BinaryOperationType.Minus,
			"=" => BinaryOperationType.Equal,
			_ => throw MakeError($"Ожидался бинарный оператор. Позиция {pos}"),
		};
		ReadNextToken();
		return binOp;
	}
	Identifier ParseIdentifier() {
		CheckType(TokenType.Identifier);
		var identifier = new Identifier(CurrentToken.Lexeme);
		ReadNextToken();
		return identifier;
	}
	Number ParseNumber() {
		CheckType(TokenType.Number);
		var number = new Number(CurrentToken.Lexeme);
		ReadNextToken();
		return number;
	}
	void CheckType(TokenType expectedType) {
		if (CurrentToken.Type != expectedType) {
			throw MakeError($"Ожидался {expectedType}. Получен {CurrentToken.Type}. {CurrentToken.Lexeme}");
		}
	}
}
