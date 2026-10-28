// The load of an entity, typed from the request: the request says what comes back — rows, only the
// asked columns, or groups — and the compiler computes that shape from the entity of the generated call.
import type { ApiError, DataSourceLoadOptionsBase, LoadGroup } from './api/model';

// Four levels of nesting, counted by the length of a tuple.
type Limit = [unknown, unknown, unknown, unknown];
type Nested<V> = NonNullable<V> extends readonly unknown[] ? never : NonNullable<V> extends object ? NonNullable<V> : never;

/** Every field path of T, the nested ones with a dot: 'Amount', 'Customer.Name'. */
export type Path<T, Seen extends unknown[] = []> = Seen extends Limit
  ? never
  : {
      [K in keyof T & string]-?: K | ([Nested<T[K]>] extends [never] ? never : `${K}.${Path<Nested<T[K]>, [...Seen, unknown]>}`);
    }[keyof T & string];

type Head<P extends string> = P extends `${infer H}.${string}` ? H : P;
type Rest<P extends string, K extends string> = P extends `${K}.${infer R}` ? R : never;

/** T with only the selected paths, nested objects kept: what the server returns for Select. */
export type Selected<T, P extends string> = {
  [K in keyof Pick<T, keyof T & Head<P>>]: K extends P
    ? T[K]
    : null extends T[K]
      ? Selected<NonNullable<T[K]>, Rest<P, K & string>> | null
      : undefined extends T[K]
        ? Selected<NonNullable<T[K]>, Rest<P, K & string>> | undefined
        : Selected<T[K], Rest<P, K & string>>;
};

export type Operator = '=' | '<>' | '<' | '<=' | '>' | '>=' | 'contains' | 'notcontains' | 'startswith' | 'endswith';
export type Condition<T> = readonly [Path<T>, Operator, string | number | boolean | null];
export type Filter<T> = Condition<T> | readonly ['!', Filter<T>] | readonly (Filter<T> | 'and' | 'or')[];
export type Interval = 'year' | 'quarter' | 'month' | 'day' | 'dayOfWeek' | 'hour' | 'minute' | 'second' | number;

export interface SortBy<T> {
  readonly Selector: Path<T>;
  readonly Desc?: boolean;
}
export interface GroupBy<T> {
  readonly Selector: Path<T>;
  readonly Desc?: boolean;
  readonly GroupInterval?: Interval;
}
export interface SummaryOf<T> {
  readonly Selector: Path<T>;
  readonly SummaryType: 'sum' | 'min' | 'max' | 'avg' | 'count';
}

/** The load options, with every field checked against the entity. */
export interface LoadRequest<T> {
  readonly Skip?: number;
  readonly Take?: number;
  readonly RequireTotalCount?: boolean;
  readonly RequireGroupCount?: boolean;
  readonly IsCountQuery?: boolean;
  readonly IsSummaryQuery?: boolean;
  readonly Filter?: Filter<T>;
  readonly Sort?: readonly SortBy<T>[];
  readonly Group?: readonly GroupBy<T>[];
  readonly Select?: readonly Path<T>[];
  readonly TotalSummary?: readonly SummaryOf<T>[];
  readonly GroupSummary?: readonly SummaryOf<T>[];
}

/** A group: its key, how many rows, the group summaries; the inner level's groups, never rows. */
export interface GroupOf<I> {
  key: LoadGroup['key'];
  items: I;
  count?: number;
  summary?: (number | null)[];
}

type Row<T, R> = R extends { readonly Select: readonly (infer S extends string)[] } ? Selected<T, S> : T;
type Groups<G> = G extends readonly [unknown, ...infer Tail] ? GroupOf<Tail extends readonly [] ? null : Groups<Tail>[]> : never;

/** What data holds for the request R on the rows of T. */
export type LoadData<T, R> = R extends { readonly Group: infer G extends readonly [unknown, ...unknown[]] } ? Groups<G>[] : Row<T, R>[];

export interface Loaded<D> {
  data: D;
  totalCount: number;
  groupCount: number;
  summary?: (number | null)[];
}

/** A generated load call — Customers_Load, Orders_Load — with its answers: the result, or the ApiError. */
export type LoadCall = (
  options?: DataSourceLoadOptionsBase | null,
) => Promise<{ status: number; data: { data: readonly unknown[] } | ApiError }>;

/** The entity a generated load call lists, read from its result: the rows of LoadResultOf<T>. */
export type EntityOf<C extends LoadCall> =
  Exclude<Awaited<ReturnType<C>>['data'], ApiError> extends { data: infer D }
    ? D extends readonly (infer X)[]
      ? Exclude<X, LoadGroup>
      : never
    : never;

/** The error the server answered, with its code and message. */
export class LoadError extends Error {
  public readonly status: number;
  public readonly error: ApiError;

  public constructor(status: number, error: ApiError) {
    super(`${String(status)} ${error.Code}: ${error.Message}`);
    this.status = status;
    this.error = error;
  }
}

// The request as the generated body: the same names, the arrays copied out of their readonly form.
const body = <T>(request: LoadRequest<T>): DataSourceLoadOptionsBase => {
  const {
    Filter: filter,
    GroupSummary: groupSummary,
    Group: group,
    Select: select,
    Sort: sort,
    TotalSummary: totalSummary,
    ...flags
  } = request;
  return {
    ...flags,
    ...(filter && { Filter: [...filter] }),
    ...(sort && { Sort: [...sort] }),
    ...(group && { Group: [...group] }),
    ...(select && { Select: [...select] }),
    ...(totalSummary && { TotalSummary: [...totalSummary] }),
    ...(groupSummary && { GroupSummary: [...groupSummary] }),
  };
};

/**
 * Runs the load and returns its result typed from the request: the overload declares that the shape of
 * data follows the request, as the server's protocol does — the server's tests and these hold it.
 */
export async function load<C extends LoadCall, const R extends LoadRequest<EntityOf<C>>>(
  call: C,
  request: R,
): Promise<Loaded<LoadData<EntityOf<C>, R>>>;
export async function load(call: LoadCall, request: LoadRequest<unknown>): Promise<unknown> {
  const { data, status } = await call(body(request));
  if ('Code' in data) {
    throw new LoadError(status, data);
  }
  return data;
}
