import { BlockItem, Expression, Node, ValidationError } from ".";
import { RealizedType, TO2Type, UNKNOWN_TYPE } from "./to2-type";
import { InputPosition, InputRange, WithPosition } from "../../parser";
import { BlockContext } from "./context";
import { SemanticToken } from "../../syntax-token";
import { isTupleType } from "./tuple-type";
import { isRecordType } from "./record-type";

export class DeclarationParameter {
  constructor(
    public readonly target: WithPosition<string>,
    public readonly source: string | undefined,
    public readonly type: TO2Type | undefined
  ) {}

  extractedType(from: RealizedType, idx: number): TO2Type | undefined {
    if (isTupleType(from)) {
      return idx < from.itemTypes.length ? from.itemTypes[idx] : undefined;
    }
    if (isRecordType(from)) {
      if (this.source)
        return from.itemTypes.find((item) => item[0] === this.source)?.[1];
      return idx < from.itemTypes.length ? from.itemTypes[idx][1] : undefined;
    }
    return undefined;
  }
}

export class DeclarationPlaceholder {}

export type DeclarationParameterOrPlaceholder =
  | DeclarationParameter
  | DeclarationPlaceholder;

export function isDeclarationParameter(
  declaration: DeclarationParameterOrPlaceholder
): declaration is DeclarationParameter {
  return (declaration as DeclarationParameter).target !== undefined;
}

export class VariableDeclaration implements Node, BlockItem {
  public readonly range: InputRange;

  constructor(
    private readonly constLetKeyword: WithPosition<"let" | "const">,
    public readonly declaration: DeclarationParameter,
    public readonly expression: Expression,
    start: InputPosition,
    end: InputPosition
  ) {
    this.range = new InputRange(start, end);
  }

  public resultType(context: BlockContext): TO2Type {
    return this.declaration.type ?? this.expression.resultType(context);
  }

  public reduceNode<T>(
    combine: (previousValue: T, node: Node) => T,
    initialValue: T
  ): T {
    return this.expression.reduceNode(combine, combine(initialValue, this));
  }

  public validateBlock(context: BlockContext): ValidationError[] {
    const errors: ValidationError[] = [];

    if (context.localVariables.has(this.declaration.target.value)) {
      errors.push({
        status: "error",
        message: `Duplicate variable ${this.declaration.target.value}`,
        range: this.declaration.target.range,
      });
    } else {
      context.localVariables.set(
        this.declaration.target.value,
        this.resultType(context)
      );
    }
    errors.push(
      ...this.expression.validateBlock(
        context,
        this.declaration.type?.realizedType(context.module)
      )
    );

    return errors;
  }

  public collectSemanticTokens(semanticTokens: SemanticToken[]): void {
    semanticTokens.push(this.constLetKeyword.range.semanticToken("keyword"));
    semanticTokens.push(
      this.declaration.target.range.semanticToken("variable", "declaration")
    );
    this.expression.collectSemanticTokens(semanticTokens);
  }
}
