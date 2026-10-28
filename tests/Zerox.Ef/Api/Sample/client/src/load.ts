// The load of an entity, typed from the request: the request says what comes back — rows, only the
// asked columns, or groups — and the compiler computes that shape from the entity of the generated call.
import type { ApiError, DataSourceLoadOptionsBase, LoadGroup } from './api/model';

type Depth = [never, 0, 1, 2, 3];
type Nested<V> = NonNullable<V> extends readonly unknown[] ? never : NonNullable<V> extends object ? NonNullable<V> : never;

/** Every field path of T, the nested ones with a dot: 'Amount', 'Customer.Name'. */
export type Path<T, D extends number = 4> = [D] extends [never] ? never : {
  [K in keyof T & string]-?: K | ([Nested<T[K]>] extends [never] ? never : `${K}.${Path<Nested<T[K]>, Depth[D]>}`)
}[keyof T & string];

type Head<P extends string> = P extends `${infer H}.${string}` ? H : P;
type Rest<P extends string, K extends string> = P extends `${K}.${infer R}` ? R : never;

/** T with only the selected paths, nested objects kept: what the server returns for Select. */
export type Selected<T, P extends string> = {
  [K in keyof Pick<T, keyof T & Head<P>>]: K extends P ? T[K]
    : null extends T[K] ? Selected<NonNullable<T[K]>, Rest<P, K & string>> | null
    : undefined extends T[K] ? Selected<NonNullable<T[K]>, Rest<P, K & string>> | undefined
    : Selected<T[K], Rest<P, K & string>>
};

export type Operator = '=' | '<>' | '<' | '<=' | '>' | '>=' | 'contains' | 'notcontains' | 'startswith' | 'endswith';
export type Condition<T> = readonly [Path<T>, Operator, string | number | boolean | null];
export type Filter<T> = Condition<T> | readonly ['!', Filter<T>] | readonly (Filter<T> | 'and' | 'or')[];
export type Interval = 'year' | 'quarter' | 'month' | 'day' | 'dayOfWeek' | 'hour' | 'minute' | 'second' | number;

export interface SortBy<T> { readonly Selector: Path<T>; readonly Desc?: boolean }
export interface GroupBy<T> { readonly Selector: Path<T>; readonly Desc?: boolean; readonly GroupInterval?: Interval }
export interface SummaryOf<T> { readonly Selector: Path<T>; readonly SummaryType: 'sum' | 'min' | 'max' | 'avg' | 'count' }

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

/** A generated load call: Customers_Load, Orders_Load. */
export type LoadCall = (options?: DataSourceLoadOptionsBase | null) => Promise<{ status: number; data: unknown }>;

/** The entity a generated load call lists, read from its answer: the rows of LoadResultOf<T>. */
export type EntityOf<C extends LoadCall> =
  Extract<Awaited<ReturnType<C>>, { status: 200 }> extends { data: { data: infer D } }
    ? D extends readonly (infer X)[] ? Exclude<X, LoadGroup> : never
    : never;

/** The error the server answered, with its code and message. */
export class LoadError extends Error {
  constructor(readonly status: number, readonly error: ApiError) {
    super(`${status} ${error.Code}: ${error.Message}`);
  }
}

const list = <V>(values: readonly V[] | undefined): V[] | undefined => (values ? [...values] : undefined);

// The request as the generated body: the same names, the arrays copied out of their readonly form.
const body = <T>(r: LoadRequest<T>): DataSourceLoadOptionsBase => {
  const options: DataSourceLoadOptionsBase = {};
  if (r.Skip !== undefined) options.Skip = r.Skip;
  if (r.Take !== undefined) options.Take = r.Take;
  if (r.RequireTotalCount !== undefined) options.RequireTotalCount = r.RequireTotalCount;
  if (r.RequireGroupCount !== undefined) options.RequireGroupCount = r.RequireGroupCount;
  if (r.IsCountQuery !== undefined) options.IsCountQuery = r.IsCountQuery;
  if (r.IsSummaryQuery !== undefined) options.IsSummaryQuery = r.IsSummaryQuery;
  if (r.Filter !== undefined) options.Filter = [...r.Filter];
  const sort = list(r.Sort), group = list(r.Group), select = list(r.Select);
  const totalSummary = list(r.TotalSummary), groupSummary = list(r.GroupSummary);
  if (sort) options.Sort = sort;
  if (group) options.Group = group;
  if (select) options.Select = select;
  if (totalSummary) options.TotalSummary = totalSummary;
  if (groupSummary) options.GroupSummary = groupSummary;
  return options;
};

/**
 * Runs the load and returns its result typed from the request. The one assertion of the client is here:
 * the shape of data is decided by the request, which the server's protocol follows — the server's tests
 * and these hold it.
 */
export const load = async <C extends LoadCall, const R extends LoadRequest<EntityOf<C>>>(
  call: C,
  request: R,
): Promise<Loaded<LoadData<EntityOf<C>, R>>> => {
  const response = await call(body(request));
  if (response.status !== 200) throw new LoadError(response.status, response.data as ApiError);
  return response.data as Loaded<LoadData<EntityOf<C>, R>>;
};
