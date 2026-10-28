// The client of the example application, generated from the document the server test keeps.
export default {
  sample: {
    input: '../openapi/Sample.json',
    output: {
      target: 'src/api/sample.ts',
      schemas: 'src/api/model',
      client: 'fetch',
      mode: 'tags-split',
      clean: true,
      override: { mutator: { path: 'src/http.ts', name: 'http' } },
    },
  },
};
