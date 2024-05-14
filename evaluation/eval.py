from flask import Flask, request, Response
from dotenv import load_dotenv
from openai import AzureOpenAI
from pydantic import BaseModel
from typing import List, Optional, Dict
import os
import json
import re
import traceback

# load environment variables from .env file
load_dotenv()

# get variables
AZURE_OPENAI_API_KEY = os.getenv("AZURE_OPENAI_API_KEY")
AZURE_OPENAI_ENDPOINT = os.getenv("AZURE_OPENAI_ENDPOINT")
AZURE_OPENAI_DEPLOYMENT = os.getenv("AZURE_OPENAI_DEPLOYMENT")

# print environment variables
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
    azure_endpoint = os.getenv("AZURE_OPENAI_ENDPOINT")
    )

# startup the Flask app
app = Flask(__name__)


class HistoryEntry(BaseModel):
    role: str
    msg: str

class Citation(BaseModel):
    ref: str

class Context(BaseModel):
    text: str
    citation: Citation

class DataSection(BaseModel):
    context: Optional[List[Context]] = None

class InputSection(BaseModel):
    user_query: Optional[str] = None
    data: Optional[DataSection] = None
    history: Optional[List[HistoryEntry]] = None

class Step(BaseModel):
    input: InputSection

class Answer(BaseModel):
    text: Optional[str] = None
    citations: Optional[List[Citation]] = None

class Inference(BaseModel):
    steps: List[Step]
    answer: Optional[Answer] = None


# define a function to extract messages from a file
def get_messages(filename: str) -> list:
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
def fill_variables(messages: list, variables: Dict[str, str]):
    for message in messages:
        for key, value in variables.items():
            message["content"] = message["content"].replace("{{" + key + "}}", value)
    return messages


def get_answer(inference: Inference) -> str:
    if not inference.answer or not inference.answer.text:
        return ""
    return inference.answer.text

def get_context(inference: Inference) -> str:
    context = ""
    if not inference.answer or not inference.answer.citations:
        return context
    for answer_citation in inference.answer.citations:
        if inference.steps[-1].input.data and inference.steps[-1].input.data.context:
            for step_context in inference.steps[-1].input.data.context:
                if answer_citation.ref == step_context.citation.ref:
                    context += step_context.text + "\n\n"
    return context

def get_history(inference: Inference) -> str:
    history = ""
    if not inference.steps[0].input.history:
        return history
    for entry in inference.steps[0].input.history:
        history += f"{entry.role}:\n{entry.msg}\n\n"
    return history


# define a function to calculate the coherence score
def calc_gpt_coherence(inference: Inference) -> dict:
    # build the prompts
    messages = get_messages("coherence.txt")
    question = inference.steps[0].input.user_query
    answer = get_answer(inference)
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
def calc_gpt_groundedness(inference: Inference) -> dict:
    # build the prompts
    messages = get_messages("groundedness.txt")
    question = inference.steps[0].input.user_query
    answer = get_answer(inference)
    context = get_context(inference)
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
def calc_gpt_relevance(inference: Inference) -> dict:
    # build the prompts
    messages = get_messages("relevance.txt")
    question = inference.steps[0].input.user_query
    answer = get_answer(inference)
    context = get_context(inference)
    history = get_history(inference)
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


# look for evaluation requests
@app.route('/api/evaluate', methods=['POST'])
def evaluate():
    try:
        # deserialize the inference payload
        inference_json = request.get_json()
        inference = Inference.model_validate(inference_json)

        # calculate coherence score
        print("calculating coherence score...")
        coherence = calc_gpt_coherence(inference)
        coherence_score = coherence["score"]
        print(f"successfully calculated coherence score as {coherence_score}.")

        # calculate groundedness score
        print("calculating groundedness score...")
        groundedness = calc_gpt_groundedness(inference)
        groundedness_score = groundedness["score"]
        print(f"successfully calculated groundedness score as {groundedness_score}.")

        # calculate relevance score
        print("calculating relevance score...")
        relevance = calc_gpt_relevance(inference)
        relevance_score = relevance["score"]
        print(f"successfully calculated relevance score as {relevance_score}.")

        # return the scores (putting scores in the header will record to exp-catalog)
        response = Response(json.dumps({
            "coherence": coherence,
            "groundedness": groundedness,
            "relevance": relevance,
        }), content_type="application/json")
        response.headers.add("x-metric-coherence", coherence_score)
        response.headers.add("x-metric-groundedness", groundedness_score)
        response.headers.add("x-metric-relevance", relevance_score)
        return response

    except Exception as e:
        print("Exception occurred:\n", traceback.format_exc())
        return {"error": str(e)}, 500

if __name__ == "__main__":
    app.run(port=6040)