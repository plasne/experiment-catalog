# Sample Evaluation Script

To install the required packages, run the following command:

```bash
pip install -r requirements.txt
```

To configure, see the below section.

To run the evaluation script, run the following command:

```bash
python eval.py
```

This will start listening to the defined queue.

## Configuration

You should create an `.env` file in the root directory or set environmental variables some other way. All of these are required:

- __AZURE_STORAGE_CONNECTION_STRING__: The connection string for the Azure Storage Account that will have the queue.

- __QUEUE_NAME__: The name of the queue in the Azure Storage Account.

- __AZURE_OPENAI_API_KEY__: The API key for the OpenAI API.

- __AZURE_OPENAI_ENDPOINT__: The endpoint for the OpenAI API.

- __AZURE_OPENAI_DEPLOYMENT__: The deployment for the OpenAI API.

- __CATALOG_API_ENDPOINT__: The endpoint for the Catalog API. (ex. <http://localhost:6030>)
