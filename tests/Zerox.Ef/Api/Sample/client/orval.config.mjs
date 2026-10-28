// The client of the example application, generated from the document the server test keeps. The
// server's address is read at every request from SAMPLE_URL.
export default {
  sample: {
    input: '../openapi/Sample.json',
    output: {
      target: 'src/api/sample.ts',
      schemas: 'src/api/model',
      client: 'fetch',
      mode: 'tags-split',
      clean: true,
      baseUrl: { runtime: "process.env['SAMPLE_URL'] ?? ''" },
    },
  },
};
