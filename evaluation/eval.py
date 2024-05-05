from azure.storage.queue import QueueServiceClient
from time import sleep
from dotenv import load_dotenv
from openai import AzureOpenAI
import os
import json
import requests
import re

# load environment variables from .env file
load_dotenv()

# get connection string and queue name from environment variables
AZURE_STORAGE_CONNECTION_STRING = os.getenv("AZURE_STORAGE_CONNECTION_STRING")
QUEUE_NAME = os.getenv("AZURE_QUEUE_NAME")
AZURE_OPENAI_API_KEY = os.getenv("AZURE_OPENAI_API_KEY")
AZURE_OPENAI_ENDPOINT = os.getenv("AZURE_OPENAI_ENDPOINT")
AZURE_OPENAI_DEPLOYMENT = os.getenv("AZURE_OPENAI_DEPLOYMENT")
CATALOG_API_ENDPOINT = os.getenv("CATALOG_API_ENDPOINT")

# print environment variables
print(f"AZURE_STORAGE_CONNECTION_STRING: {'(set)' if AZURE_STORAGE_CONNECTION_STRING else '(not-set)'}")
print(f"AZURE_QUEUE_NAME: {QUEUE_NAME}")
print(f"AZURE_OPENAI_API_KEY: {'(set)' if AZURE_OPENAI_API_KEY else '(not-set)'}")
print(f"AZURE_OPENAI_ENDPOINT: {AZURE_OPENAI_ENDPOINT}")
print(f"AZURE_OPENAI_DEPLOYMENT: {AZURE_OPENAI_DEPLOYMENT}")
print(f"CATALOG_API_ENDPOINT: {CATALOG_API_ENDPOINT}")

# check if the required environment variables are set
if not AZURE_STORAGE_CONNECTION_STRING:
    raise Exception("AZURE_STORAGE_CONNECTION_STRING environment variable is not set.")
if not QUEUE_NAME:
    raise Exception("AZURE_QUEUE_NAME environment variable is not set.")
if not AZURE_OPENAI_API_KEY:
    raise Exception("AZURE_OPENAI_API_KEY environment variable is not set.")
if not AZURE_OPENAI_ENDPOINT:
    raise Exception("AZURE_OPENAI_ENDPOINT environment variable is not set.")
if not AZURE_OPENAI_DEPLOYMENT:
    raise Exception("AZURE_OPENAI_DEPLOYMENT environment variable is not set.")
if not CATALOG_API_ENDPOINT:
    raise Exception("CATALOG_API_ENDPOINT environment variable is not set.")

# create the AzureOpenAI object
client = AzureOpenAI(
    api_key=os.getenv("AZURE_OPENAI_API_KEY"),
    api_version="2024-02-01",
    azure_endpoint = os.getenv("AZURE_OPENAI_ENDPOINT")
    )

# create a QueueServiceClient object that will be used to create a QueueClient object
queue_service_client = QueueServiceClient.from_connection_string(AZURE_STORAGE_CONNECTION_STRING)

# create a QueueClient object which will be used to interact with the queue
queue_client = queue_service_client.get_queue_client(QUEUE_NAME)


# define a function to extract messages from a file
def get_messages(filename):
    with open(filename, 'r') as file:
        lines = file.readlines()

    messages = []
    current_role = None
    current_content = ""

    for line in lines:
        if re.match(r'(system|user|assistant):', line):
            if current_role:
                messages.append({"role": current_role, "content": current_content.strip()})
            current_role = line[:-2]
            current_content = ""
        else:
            current_content += line

    if current_role:
        messages.append({"role": current_role, "content": current_content.strip()})

    return messages


# define a function to replace variables
def fill_variables(messages, variables):
    for message in messages:
        for key, value in variables.items():
            message["content"] = message["content"].replace("{{" + key + "}}", value)
    return messages


# define a function to calculate the coherence score
def calc_gpt_coherence(inference) -> dict:
    # build the prompts
    messages = get_messages("coherence.txt")
    question = inference["history"][-1]["msg"]
    answer = inference["answer"]
    messages = fill_variables(
        messages,
        {
            "question": question,
            "answer": answer,
        }
    )

    # send it through the LLM
    response = client.chat.completions.create(
        model=AZURE_OPENAI_DEPLOYMENT,
        messages=messages
    )

    # extract the rating
    print (response.choices[0].message.content)
    payload = json.loads(response.choices[0].message.content)
    return payload


# define a function to calculate the groundedness score
def calc_gpt_groundedness(inference) -> dict:
    # build the prompts
    messages = get_messages("groundedness.txt")
    question = inference["history"][-1]["msg"]
    context = ' '.join([item['text'] for item in inference["content"]])
    answer = inference["answer"]
    messages = fill_variables(
        messages,
        {
            "question": question,
            "answer": answer,
            "context": context,
        }
    )

    # send it through the LLM
    response = client.chat.completions.create(
        model=AZURE_OPENAI_DEPLOYMENT,
        messages=messages
    )

    # extract the rating
    print (response.choices[0].message.content)
    payload = json.loads(response.choices[0].message.content)
    return payload


# define a function to calculate the relevance score
def calc_gpt_relevance(inference) -> dict:
    # build the prompts
    messages = get_messages("relevance.txt")
    question = inference["history"][-1]["msg"]
    context = ' '.join([item['text'] for item in inference["content"]])
    answer = inference["answer"]
    history = ' '.join([item['msg'] for item in inference["history"][:-1]])
    messages = fill_variables(
        messages, 
        {
            "question": question, 
            "answer": answer, 
            "context": context, 
            "history": history,
        }
    )

    # send it through the LLM
    response = client.chat.completions.create(
        model=AZURE_OPENAI_DEPLOYMENT,
        messages=messages
    )

    # extract the rating
    print (response.choices[0].message.content)
    payload = json.loads(response.choices[0].message.content)
    return payload


# poll the queue for messages
print(f"starting to listen to queue {QUEUE_NAME}...")
while True:
    try:
        # get the next message from the queue
        messages = queue_client.receive_messages(max_messages=1, visibility_timeout=120)

        # process the messages
        for msg in messages:
            # deserialize as JSON
            request = json.loads(msg.content)
            project = request['project']
            experiment = request['experiment']
            ref = request['ref']
            set = request['set']
            print(f"received request for evaluation of ref: {ref}...")

            # get the inference payload
            catalog_uri = request["inference_uri"]
            print(f"attempting to download inference payload from {catalog_uri}...")
            inference_response = requests.get(catalog_uri)
            if inference_response.status_code != 200:
                raise Exception(f"{inference_response.status_code}: {inference_response.text}")
            print(f"succesfully downloaded inference payload for ref: {ref}.")

            # deserialize the inference payload
            inference = inference_response.json()

            # calculate coherence score
            print(f"calculating coherence score for ref: {ref}...")
            coherence = calc_gpt_coherence(inference)
            inference["coherence"] = coherence
            coherence_score = coherence["score"]
            print(f"successfully calculated coherence score for ref: {ref} as {coherence_score}.")

            # calculate groundedness score
            print(f"calculating groundedness score for ref: {ref}...")
            groundedness = calc_gpt_groundedness(inference)
            inference["groundedness"] = groundedness
            groundedness_score = groundedness["score"]
            print(f"successfully calculated groundedness score for ref: {ref} as {groundedness_score}.")

            # calculate relevance score
            print(f"calculating relevance score for ref: {ref}...")
            relevance = calc_gpt_relevance(inference)
            inference["relevance"] = relevance
            relevance_score = relevance["score"]
            print(f"successfully calculated relevance score for ref: {ref} as {relevance_score}.")

            # upload the evaluation file
            evaluation_uri = request["evaluation_uri"]
            print(f"attempting to upload evaluation payload for ref: {ref} to {evaluation_uri}...")
            evaluation_headers = {
                "Content-Type": "application/json",
                "x-ms-blob-type": "BlockBlob",
            }
            evaluation_response = requests.put(evaluation_uri, headers=evaluation_headers, data=json.dumps(inference, indent=4))
            if evaluation_response.status_code != 201:
                raise Exception(f"{evaluation_response.status_code}: {evaluation_response.text}")
            print(f"successfully uploaded evaluation payload for ref: {ref} to {evaluation_uri}.")

            # post the results to the catalog
            print(f"attempting to post results for ref: {ref} to experiment catalog...")
            catalog_uri = f"{CATALOG_API_ENDPOINT}/api/projects/{project}/experiments/{experiment}/results"
            catalog_headers = {"Content-Type": "application/json"}
            catalog_payload = {
                "ref": ref,
                "set": set,
                "result_uri": evaluation_uri,
                "metrics": {
                    "gpt-coherance": { "value": coherence_score },
                    "gpt-groundedness": { "value": groundedness_score },
                    "gpt-relevance": { "value": relevance_score }
                }
            }
            catalog_response = requests.post(catalog_uri, headers=catalog_headers, data=json.dumps(catalog_payload))
            if catalog_response.status_code != 200:
                raise Exception(f"{catalog_response.status_code}: {catalog_response.text}")
            print(f"successfully posted results for ref: {ref} to experiment catalog.")

            # delete the message as it has been processed
            print(f"deleting message for ref: {ref} because processing was successful...")
            queue_client.delete_message(msg)
            print(f"successfully deleted message for ref: {ref}.")

        # sleep for a bit before polling the queue again
        if not messages:
            sleep(5)

    except Exception as e:
        print(e)
        sleep(5)