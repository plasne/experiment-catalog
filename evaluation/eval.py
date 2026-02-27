from flask import Flask, request, Response
from dotenv import load_dotenv
from openai import AzureOpenAI, RateLimitError, APIError
from typing import Dict
import os
import json
import re
import traceback

# load environment variables from .env file
load_dotenv()

# get variables
PORT = os.getenv("PORT", 6040)
AZURE_OPENAI_API_KEY = os.getenv("AZURE_OPENAI_API_KEY")
AZURE_OPENAI_ENDPOINT = os.getenv("AZURE_OPENAI_ENDPOINT")
AZURE_OPENAI_DEPLOYMENT = os.getenv("AZURE_OPENAI_DEPLOYMENT")

# print environment variables
print(f"PORT: {PORT}")
print(f"AZURE_OPENAI_API_KEY: {'(set)' if AZURE_OPENAI_API_KEY else '(not-set)'}")
print(f"AZURE_OPENAI_ENDPOINT: {AZURE_OPENAI_ENDPOINT}")
print(f"AZURE_OPENAI_DEPLOYMENT: {AZURE_OPENAI_DEPLOYMENT}")

# check if the required environment variables are set
if not AZURE_OPENAI_API_KEY:
    raise ValueError("AZURE_OPENAI_API_KEY environment variable is not set.")
if not AZURE_OPENAI_ENDPOINT:
    raise ValueError("AZURE_OPENAI_ENDPOINT environment variable is not set.")
if not AZURE_OPENAI_DEPLOYMENT:
    raise ValueError("AZURE_OPENAI_DEPLOYMENT environment variable is not set.")

# create the AzureOpenAI object
client = AzureOpenAI(
    api_key=os.getenv("AZURE_OPENAI_API_KEY"),
    api_version="2024-02-01",
    azure_endpoint=os.getenv("AZURE_OPENAI_ENDPOINT"),
)

# startup the Flask app
app = Flask(__name__)


# define a function to extract messages from a file
def get_messages(filename: str) -> list:
    with open(filename, "r") as file:
        lines = file.readlines()

    messages = []
    current_role = None
    current_content = ""

    for line in lines:
        if re.match(r"(system|user|assistant):", line):
            if current_role:
                messages.append(
                    {"role": current_role, "content": current_content.strip()}
                )
            current_role = line[:-2]
            current_content = ""
        else:
            current_content += line

    if current_role:
        messages.append({"role": current_role, "content": current_content.strip()})

    return messages


# define a function to replace variables
def fill_variables(messages: list, variables: Dict[str, str]):
    for message in messages:
        for key, value in variables.items():
            message["content"] = message["content"].replace("{{" + key + "}}", value)
    return messages


# define a function to calculate the coherence score
def calc_gpt_coherence(question: str, answer: str) -> dict:
    # build the prompts
    messages = get_messages("coherence.txt")
    messages = fill_variables(
        messages,
        {
            "question": question,
            "answer": answer,
        },
    )

    # send it through the LLM
    response = client.chat.completions.create(
        model=AZURE_OPENAI_DEPLOYMENT, messages=messages
    )

    # extract the rating
    print(response.choices[0].message.content)
    payload = json.loads(response.choices[0].message.content)
    return payload


# define a function to calculate the groundedness score
def calc_gpt_groundedness(question: str, answer: str, context: str) -> dict:
    # build the prompts
    messages = get_messages("groundedness.txt")
    messages = fill_variables(
        messages,
        {
            "question": question,
            "answer": answer,
            "context": context,
        },
    )

    # send it through the LLM
    response = client.chat.completions.create(
        model=AZURE_OPENAI_DEPLOYMENT, messages=messages
    )

    # extract the rating
    print(response.choices[0].message.content)
    payload = json.loads(response.choices[0].message.content)
    return payload


# define a function to calculate the relevance score
def calc_gpt_relevance(question: str, answer: str, context: str, history: str) -> dict:
    # build the prompts
    messages = get_messages("relevance.txt")
    messages = fill_variables(
        messages,
        {
            "question": question,
            "answer": answer,
            "context": context,
            "history": history,
        },
    )

    # send it through the LLM
    response = client.chat.completions.create(
        model=AZURE_OPENAI_DEPLOYMENT, messages=messages
    )

    # extract the rating
    print(response.choices[0].message.content)
    payload = json.loads(response.choices[0].message.content)
    return payload


# look for evaluation requests
@app.route("/api/evaluate", methods=["POST"])
def evaluate():
    try:
        # deserialize the payload
        payload_json = request.get_json()
        ground_truth = payload_json["ground_truth"]
        inference = payload_json["inference"]

        # validate input
        if "user_query" not in ground_truth:
            raise ValueError("ground_truth must contain 'user_query'.")
        if "answer" not in inference:
            raise ValueError("inference must contain 'answer'.")
        if "text" not in inference["answer"]:
            raise ValueError("inference['answer'] must contain 'text'.")

        # extract the necessary information
        question = ground_truth["user_query"]
        answer = inference["answer"]["text"]
        context = "\n\n".join(
            [obj["text"] for obj in inference["answer"].get("context", [])]
        )
        history = "\n\n".join(
            [obj["role"] + ": " + obj["msg"] for obj in ground_truth.get("history", [])]
        )

        # calculate coherence score
        print("calculating coherence score...")
        coherence = calc_gpt_coherence(question, answer)
        coherence_score = coherence["score"]
        print(f"successfully calculated coherence score as {coherence_score}.")

        # calculate groundedness score
        print("calculating groundedness score...")
        groundedness = calc_gpt_groundedness(question, answer, context)
        groundedness_score = groundedness["score"]
        print(f"successfully calculated groundedness score as {groundedness_score}.")

        # calculate relevance score
        print("calculating relevance score...")
        relevance = calc_gpt_relevance(question, answer, context, history)
        relevance_score = relevance["score"]
        print(f"successfully calculated relevance score as {relevance_score}.")

        # return the scores (putting scores in the header will record to exp-catalog)
        response = Response(
            json.dumps(
                {
                    "coherence": coherence,
                    "groundedness": groundedness,
                    "relevance": relevance,
                }
            ),
            content_type="application/json",
        )
        response.headers.add("x-metric-coherence", coherence_score)
        response.headers.add("x-metric-groundedness", groundedness_score)
        response.headers.add("x-metric-relevance", relevance_score)
        return response

    except ValueError as e:
        return {"error": str(e)}, 400

    except RateLimitError as e:
        response = Response(status=429)
        if hasattr(e, "retry_after"):
            response.headers.add("Retry-After", e.retry_after)
        return response

    except APIError as e:
        if hasattr(e, "retry_after"):
            response = Response(status=429)
            response.headers.add("Retry-After", e.retry_after)
            return response

        print("Exception occurred:\n", traceback.format_exc())
        return {"error": str(e)}, 500

    except Exception as e:
        print("Exception occurred:\n", traceback.format_exc())
        return {"error": str(e)}, 500


if __name__ == "__main__":
    app.run(port=PORT)
